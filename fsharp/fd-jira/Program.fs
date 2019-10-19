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
      ctx.log.Information ("Changed Issues Retreived") 
      (jsonToStr >> printfn "\nresult:\n%s") jd
    | Error e ->
      ctx.log.Error ("Error {e}", e)
  }

let getIssue ctx issue =
  async {
    match! JiraApi.getIssue ctx BASE_URL issue with
    | Ok jd ->
      // printfn "\nIssue:\n%s\n" (jsonToStr jd) 
      return Issue.fromJson jd.RootElement
    | Error e ->
      ctx.log.Error ("Error: {e}", e)
      return Error e
  }

let printIssues ctx startAt count =
  [1..count] 
  |> List.map(fun n -> sprintf "RCTFD-%i" (startAt + n))
  |> List.iter(fun id -> 
    match getIssue ctx id |> Async.RunSynchronously with
    | Ok issue -> printfn "%s\nIssue: %s" div (string issue)
    | Error e -> ctx.log.Error ("Error: {e}", e)     
  )  

let printFields ctx =
  match JiraApi.getFields ctx BASE_URL |> Async.RunSynchronously with
  | Ok jd ->
    printfn "%s\nfields:\n%s" div (jsonToStr jd)
  | Error e -> ctx.log.Error ("Error: {e}", e)

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
  ctx.log.Information "Startup"
  if argv |> Seq.length > 0 then 
    ctx.log.Information("Command line args: {0}", (String.Join(' ', argv)))
  else
    ctx.log.Information("No command line args passed")
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
        ctx.log.Error("Missing parse case for verb type: {0}", (t.GetType().Name))
        Error (sprintf "Missinc parse case for verb type: %s" (t.GetType().Name))
    | :? CommandLine.NotParsed<obj> as np -> 
      ctx.log.Warning("Not parsed: {@0}", np)
      Error (
        sprintf "Not parsed: %s" 
          (np.Errors 
          |> Seq.map(fun e -> string e.Tag) 
          |> (fun errs -> String.Join(',', errs))))
    | _ -> 
      let msg = "Unexpected CLI parser response"
      ctx.log.Fatal msg
      Error msg

  match opts with
  | Ok opts ->
    Environment.GetEnvironmentVariable("JIRA_CREDS")
    |> Result.ofObj "No Creds Found!"
    |> Result.map (fun cr ->
        ctx.log.Information "Credentials secured"
        let ctx = ctx.SetCreds cr
        commandProcessor ctx opts
      )
    |> (function
        | Ok _ -> ()
        | Error e -> ctx.log.Error ("Error: {e}", e))
  | Error _ -> ()

  ctx.log.Information "Program end"
  0
