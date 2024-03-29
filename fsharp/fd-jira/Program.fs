﻿open System

open System.Text.Json
open JsonSerialization
open CommandLine
open Microsoft.FSharp.Core
open Cli
open Fons.Components
open FonsCli
open Fons.Internal

[<Literal>]
let BASE_URL = "https://jira.walmart.com"
let PROJECT = "RCTFD"

let jsonSerializerOptions =
  let jo = JsonSerializerOptions()
  jo.WriteIndented <- true
  jo

let div = String('-', 80)

let jsonToStr (jd:JsonDocument) = JsonSerializer.Serialize(jd.RootElement, jsonSerializerOptions)

/// Returns the Issue model as populated from the Jira API json
let getIssue ctx issue =
  async {
    match! JiraApi.getIssue ctx BASE_URL issue with
    | Ok jd ->
      return Issue.fromJson jd.RootElement
    | Error e ->
      return Error e
  }

/// Prints issues starting at an issue number for the specified count. Issues come
/// from the Jira API, not the cached database
let printIssues ctx startAt count =
  /// Parses out number from the full issue id (ex: RCTFD-1234 -> 1234). We need this
  /// because we're going to start at this number and run a range for the specified count
  let idNumFromId (s:string) =
    let parts = s.Split '-' 
    if parts |> Seq.length = 2 then 
      match Int32.TryParse parts.[1] with
      | (true, n) -> n
      | (false, _) -> 0
    else 0
  let getIssueWithId id = async {
    let! res = getIssue ctx id
    return (id, res)
  }
  let ans = 
    [1..count] 
    |> List.map(fun n -> 
      let id = sprintf "%s-%i" PROJECT (startAt + n)
      getIssueWithId id)
    |> Async.Parallel 
    |> Async.RunSynchronously
    |> Seq.groupBy(function (_, Ok _) -> 0 | (_, Error _) -> 1)
    |> Seq.sortBy(fun (group, _) -> group)  
    |> Map.ofSeq
  if Map.containsKey 0 ans then
    printfn "Tickets: "
    ans.[0]
    |> Seq.sortBy (function (id, Ok _) -> idNumFromId id | _ -> 0) 
    |> Seq.iter (function
      | (_, Ok issue) -> 
        match Database.saveIssue ctx issue with
        | Ok () -> printfn "saved: %s" (string issue)
        | Error e -> printfn "not-saved: %s" e
      | _ -> ()
    )
  else 
    printfn "\nNo tickets found for selected range of IDs %i to %i" startAt (startAt + count)
  if Map.containsKey 1 ans then 
    printfn "\n%i ID numbers were not found" (Seq.length ans.[1])  
  
let printFields ctx =
  match JiraApi.getFields ctx BASE_URL |> Async.RunSynchronously with
  | Ok jd   -> printfn "%s\nfields:\n%s" div (jsonToStr jd)
  | Error e -> ctx.log.Error ("printFields|{e}", e)

let printPassThru ctx query =
  match JiraApi.passThru ctx BASE_URL query |> Async.RunSynchronously with 
  | Ok jd   -> printfn "\nAPI Result:\n%s\n%s\n" div (jsonToStr jd)
  | Error e -> printfn "API Error: %s" e

let printIssueFromDb ctx id =
  let issue = 
    match id with 
    | Parser.Ast.IssueIdent.Num n -> sprintf "%s-%s" PROJECT n
    | Parser.Ast.IssueIdent.FullId id -> id 
  match Database.tryGetIssue ctx issue with
  | Some issue -> printfn "%s" (issue.ToStringLong())
  | None  -> printfn "Issue %s not found" issue

let printBulk ctx since startAt maxCount =
  match JiraApi.getChangedIssues ctx BASE_URL since startAt maxCount |> Async.RunSynchronously with 
  | Ok jd   -> printfn "\n%s\n" (jsonToStr jd) 
  | Error e -> printfn "API Error: %s" e
  ()

let printFindResults ctx =
  let (count,items) = Database.sampleQuery ctx
  items
  |> Seq.iter (fun i -> printfn "%s\n%s\n" div (i.ToStringLong()))
  printfn "%i items found" count

/// Syncs issues from the Jira API into the local database cache. If
/// the lastUpdate date is supplied it is used as a parameter, getting
/// issues from Jira that were modified after that date. Otherwise, it
/// checks for the last update date stored in the cache. If there is no
/// such date there, then MinDate is used.
let performSync ctx (lastUpdate: DateTime option) =
  /// fn to save a key. Will be passed to the update processor which will use it
  /// on each found issue. We'll pull the issue from the API, deser and save it
  let getAndSave key = async {
    let! jd = JiraApi.getIssue ctx BASE_URL key
    match jd with
    | Ok jd -> 
      match Issue.fromJson jd.RootElement with
      | Ok issue ->
        match Database.saveIssue ctx issue with
        | Ok () -> ()
        | Error e ->
          printfn "Error saving key %s: %s" key e
      | Error e ->
        printfn "Error deserializing saved json into an issue with key %s: %s" key e
    | Error e ->
      printfn "Error getting issue %s: %s" key e
  }

  let lastUpdate  =
    match lastUpdate with 
    | Some d -> 
      ctx.log.Debug("performSync|Using explicit date: {since}", d)
      d
    | None ->
      match Database.tryGetSystemState ctx PROJECT with 
      | Some ss -> 
        ctx.log.Debug("performSync|Using SystemState last update: {since}", ss.issuesUpdated)
        ss.issuesUpdated
      | None ->
        ctx.log.Debug("performSync|Using DateTime.MinValue") 
        DateTime.MinValue
  printfn "\nSync since: %s\n" (lastUpdate.ToString("yyyy-MM-ddThh:mm"))

  // callback for reporting progress on a batch level
  let progress completed _ total =
    render [updateSyncStatus completed total] RenderState.init |> ignore

  let result = 
    JiraApi.processChangedIssues ctx BASE_URL lastUpdate 50 getAndSave progress
    |> Async.RunSynchronously

  printfn "\n\nFound %i issues updated since %s, new last updated date is %s. Saving."
    result.count
    (string lastUpdate)
    (string result.updated)

  match Database.saveSystemState ctx PROJECT result.updated with
  | Ok () -> printfn "Done"
  | Error err ->
    printfn "Error saving system state:%s\nLast update time is not saved.\n" err

let printCount ctx = 
  let c = Database.countIssues ctx
  printfn "Issue Count: %i" c

let printLastUpd ctx =
  match Database.tryGetSystemState ctx PROJECT with 
  | Some ss ->
    printfn "Last updated project %s: %s" ss.project (string ss.issuesUpdated)
  | None ->
    printfn "Project %s has never been synchronized" PROJECT

let printHelp () =
  let hlp = """
Available Commands:

  count             - displays the count of cached items
  range             - pulls a range of items from the Jira API
  last update       - displays the last update date for sync
  get [id]          - gets the ID from cache. ID can be the full ID or the number
  sync [yyyy/mm/dd] - syncs cache to Jira API. If the  date is not supplied uses
                      the latest sync date from issues in the cache
  help              - displays this help
  exit              - exits the CLI

"""
  printfn "%s" hlp

let commandProcessor ctx opts =
  match opts with
  | Opts.Count _    -> ()
  | Opts.PassThru p -> printPassThru   ctx p.query
  | Opts.Get _      -> ()
  | Opts.Bulk b     -> printBulk       ctx b.changedSince b.startAt b.maxCount
  | Opts.Sync s     -> performSync     ctx s.lastUpdate
  | Opts.Range r    -> printIssues     ctx r.startAt r.count
  | Opts.Unknown    -> printfn "Unknown. I don't know what you're asking."
  0

let processArgs ctx argv =
  let argv = if argv |> Array.isEmpty then [|"--help"|] else argv
  match getCliOpts ctx argv with
  | Ok opts ->
    Environment.GetEnvironmentVariable("JIRA_CREDS")
    |> Result.ofObj "No Creds Found!"
    |> Result.map (fun cr ->
        ctx.log.Information "processArgs|creds|Credentials secured"
        let ctx = ctx.SetCreds cr
        commandProcessor ctx opts
      )
    |> (function
        | Ok _ -> ()
        | Error e -> ctx.log.Error ("main|creds|{e}", e))
  | Error _ -> ()

  
module CmdProc =
  open Parser 
  open FParsec    
  open Parser.Ast

  let prompt () = 
    printf "\n"
    render [getCmdLinePrompt()] RenderState.init |> ignore
    Console.ReadLine()

  let rec parseCmdLoop ctx (input:string) =
    if String.IsNullOrWhiteSpace(input) then parseCmdLoop ctx (prompt())
    else
      interpret ctx input false
  
  and interpret ctx input single =
    match run cmdParser input with 
    | Success(Command.Count,_,_) -> 
      printCount ctx
      if not single then parseCmdLoop ctx (prompt())
    | Success(Command.Get id,_,_) ->
      printIssueFromDb ctx id
      if not single then parseCmdLoop ctx (prompt())
    | Success(Command.LastUpd,_,_) ->
      printLastUpd ctx
      if not single then parseCmdLoop ctx (prompt())
    | Success(Command.Sync since,_,_) ->
      performSync ctx since
      if not single then parseCmdLoop ctx (prompt())
    | Success(Command.Range (startAt, count),_,_) ->
      // printIssues ctx startAt count
      printFindResults ctx
      if not single then parseCmdLoop ctx (prompt())
    | Success(Command.Help,_,_) ->
      printHelp ()
      if not single then parseCmdLoop ctx (prompt())
    | Success(Command.Exit,_,_) ->
      printfn "bye!\n"
    | Failure(msg,_,_) ->
      printfn "%s" msg
      if not single then parseCmdLoop ctx (prompt())

[<EntryPoint>]    
let main argv =
  let ctx = Prelude.initCtx
  ctx.log.Information "main|Startup"
  Database.logDbInfo ctx
  let ctxOpt =
    Environment.GetEnvironmentVariable("JIRA_CREDS")
    |> Result.ofObj "No Creds Found!"
    |> Result.map (fun cr ->
        ctx.log.Information "processArgs|creds|Credentials secured"
        ctx.SetCreds cr
      )
  match ctxOpt with
  | Ok ctx ->
    if not (Array.isEmpty argv) then 
      CmdProc.interpret ctx (String.Join(' ', argv)) true
    else
      render [switchToAlt; clrScreen; pos 0 0] RenderState.init |> ignore
      CmdProc.parseCmdLoop ctx (String.Join(' ', argv))
      render [switchToMain] RenderState.init |> ignore 
  | Error e ->
    printfn "%s" e

  printfn ""

  ctx.log.Information "main|end"
  0
