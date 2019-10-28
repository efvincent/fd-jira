open System

open System.Text.Json
open JsonSerialization
open CommandLine
open Microsoft.FSharp.Core
open Cli

[<Literal>]
let BASE_URL = "https://jira.walmart.com"
let PROJECT = "RCTFD"

let jsonSerializerOptions =
  let jo = JsonSerializerOptions()
  jo.WriteIndented <- true
  jo

let div = String('-', 80)

let jsonToStr (jd:JsonDocument) = JsonSerializer.Serialize(jd.RootElement, jsonSerializerOptions)

let getUpdatedItems ctx =
  async {
    match JiraApi.getChangedIssues ctx BASE_URL DateTimeOffset.MinValue 0 10 |> Async.RunSynchronously with
    | Ok jd ->
      (jsonToStr >> printfn "\nresult:\n%s") jd
    | Error e ->
      ctx.log.Error ("getUpdatedItems|{e}", e)
  }

let getIssue ctx ident =
  async {
    let issue = 
      match ident with 
      | Parser.Ast.IssueIdent.Num n -> sprintf "%s-%s" PROJECT n
      | Parser.Ast.IssueIdent.FullId id -> id 

    match! JiraApi.getIssue ctx BASE_URL issue with
    | Ok (jd) ->
      return Issue.fromJson jd.RootElement
    | Error e ->
      return Error e
  }

let getIssue2 ctx issue =
  async {
    match! JiraApi.getIssue ctx BASE_URL issue with
    | Ok jd ->
      return Issue.fromJson jd.RootElement
    | Error e ->
      return Error e
  }

let printIssues ctx startAt count =
  let idNumFromId (s:string) =
    let parts = s.Split '-' 
    if parts |> Seq.length = 2 then 
      match Int32.TryParse parts.[1] with
      | (true, n) -> n
      | (false, _) -> 0
    else 0
  let getIssueWithId id = async {
    let! res = getIssue2 ctx id
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

let performSync ctx lastUpdate =
  // fn to save a key. Will be passed to the update processor which will use it
  // on each found issue. We'll pull the issue from the API, deser and save it
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

  // callback for reporting progress on a batch level
  let progress completed working total =
    printfn "completed: %5i current: %3i total: %6i" completed working total

  let result = 
    JiraApi.processChangedIssues ctx BASE_URL lastUpdate 50 getAndSave progress
    |> Async.RunSynchronously

  printfn "Found %i issues updated since %s, new last updated date is %s. Saving."
    result.count
    (string lastUpdate)
    (string result.updated)
  printfn "Done"

let printCount ctx = 
  let c = Database.countIssues ctx
  printfn "Issue Count: %i" c

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
    printf "\nFD-JIRA > "
    Console.ReadLine()

  let rec parsecCmdLoop ctx (input:string) =
    if String.IsNullOrWhiteSpace(input) then parsecCmdLoop ctx (prompt())
    else
      interpret ctx input false
  
  and interpret ctx input single =
    match run cmdParser input with 
    | Success(Command.Count,_,_) -> 
      printCount ctx
      if not single then parsecCmdLoop ctx (prompt())
    | Success(Command.Get id,_,_) ->
      printIssueFromDb ctx id
      if not single then parsecCmdLoop ctx (prompt())
    | Success(Command.Sync since,_,_) ->
      performSync ctx since
      if not single then parsecCmdLoop ctx (prompt())
    | Success(Command.Range (startAt, count),_,_) ->
      // printIssues ctx startAt count
      printFindResults ctx
      if not single then parsecCmdLoop ctx (prompt())
    | Success(Command.Help,_,_) ->
      printfn "understood: Help command"
      if not single then parsecCmdLoop ctx (prompt())
    | Success(Command.Exit,_,_) ->
      printfn "bye!\n"
    | Failure(msg,_,_) ->
      printfn "%s" msg
      if not single then parsecCmdLoop ctx (prompt())

[<EntryPoint>]    
let main argv =
  let ctx = Prelude.initCtx
  ctx.log.Information "main|Startup"
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
      CmdProc.parsecCmdLoop ctx (String.Join(' ', argv))
  | Error e ->
    printfn "%s" e

  printfn ""

  ctx.log.Information "main|end"
  0
