open System

open System.Text.Json
open JsonSerialization
open CommandLine
open Microsoft.FSharp.Core
open Cli

[<Literal>]
let BASE_URL = "https://jira.walmart.com/rest/api/2"

let jsonSerializerOptions =
  let jo = JsonSerializerOptions()
  jo.WriteIndented <- true
  jo

let div = String('-', 80)

let jsonToStr (jd:JsonDocument) = JsonSerializer.Serialize(jd.RootElement, jsonSerializerOptions)

let getUpdatedItems ctx =
  async {
    match JiraApi.getChangedIssues ctx BASE_URL 0 |> Async.RunSynchronously with
    | Ok jd ->
      (jsonToStr >> printfn "\nresult:\n%s") jd
    | Error e ->
      ctx.log.Error ("getUpdatedItems|{e}", e)
  }

let getIssue ctx issue =
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
    let! res = getIssue ctx id
    return (id, res)
  }
  let ans = 
    [1..count] 
    |> List.map(fun n -> 
      let id = sprintf "RCTFD-%i" (startAt + n)
      getIssueWithId id)
    |> Async.Parallel 
    |> Async.RunSynchronously
    |> Seq.groupBy(function (_, Ok _) -> 0 | (_, Error _) -> 1)
    |> Seq.sortBy(fun (group, _) -> group)  
    |> Map.ofSeq
  if Map.containsKey 0 ans then
    printfn "Issues: "
    ans.[0]
    |> Seq.sortBy (function (id, Ok _) -> idNumFromId id | _ -> 0) 
    |> Seq.iter (function
      | (_, Ok issue) -> printfn "%s\n%s" div (string issue)
      | _ -> ()
    )
  if Map.containsKey 1 ans then 
    printfn "Errors:"
    ans.[1]
    |> Seq.iter (function
    | (id, Error e) -> printfn "Issue: %s - %s" id e
    | _ -> ()
  )
  
let printFields ctx =
  match JiraApi.getFields ctx BASE_URL |> Async.RunSynchronously with
  | Ok jd ->
    printfn "%s\nfields:\n%s" div (jsonToStr jd)
  | Error e -> ctx.log.Error ("printFields|{e}", e)

let commandProcessor ctx opts =
  match opts with
  | Opts.Basic b ->
    if b.version then
      printfn "Yes Billy there is a version."
    else
      printfn "I don't know what you want from me."
  | Opts.Range r ->
    printIssues ctx r.startAt r.count
  | Opts.Unknown ->
    printfn "Unknown. I don't know what you're asking."
  0

[<EntryPoint>]    
let main argv =
  let ctx = Prelude.initCtx
  ctx.log.Information "main|Startup"
  if argv |> Seq.length > 0 then 
    ctx.log.Information("main|args|{0}", (String.Join(' ', argv)))
  else
    ctx.log.Information("main|No args passed")
  let cliResult =
    CommandLine
      .Parser
      .Default
      .ParseArguments<BasicOpts,RangeOpts> argv 
  let opts =    
    match cliResult with
    | :? CommandLine.Parsed<obj> as verb ->
      match verb.Value with
      | :? BasicOpts as opts -> Ok <| Opts.Basic opts
      | :? RangeOpts as opts -> Ok <| Opts.Range opts
      | t -> 
        ctx.log.Error("args|Missing parse case for verb type|{0}", (t.GetType().Name))
        Error (sprintf "Missinc parse case for verb type: %s" (t.GetType().Name))
    | :? CommandLine.NotParsed<obj> as np -> 
      ctx.log.Warning("args|Not parsed|{@0}", np)
      Error (
        sprintf "Not parsed: %s" 
          (np.Errors 
          |> Seq.map(fun e -> string e.Tag) 
          |> (fun errs -> String.Join(',', errs))))
    | _ -> 
      let msg = "Unexpected CLI parser response"
      ctx.log.Fatal("args|{0}", msg)
      Error msg

  match opts with
  | Ok opts ->
    Environment.GetEnvironmentVariable("JIRA_CREDS")
    |> Result.ofObj "No Creds Found!"
    |> Result.map (fun cr ->
        ctx.log.Information "main|creds|Credentials secured"
        let ctx = ctx.SetCreds cr
        commandProcessor ctx opts
      )
    |> (function
        | Ok _ -> ()
        | Error e -> ctx.log.Error ("main|creds|{e}", e))
  | Error _ -> ()

  ctx.log.Information "main|end"
  0
