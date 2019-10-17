open System

open System.Text.Json
open JsonSerialization
open Microsoft.FSharp.Core

[<Literal>]
let BASE_URL = "https://jira.walmart.com/rest/api/2"

let getUpdatedItems creds =
  async {
    match JiraApi.getChangedIssues creds BASE_URL 0 |> Async.RunSynchronously with
    | Ok jd ->
      let rs =
        let opts = JsonSerializerOptions()
        opts.WriteIndented <- true 
        JsonSerializer.Serialize(jd.RootElement, opts)
      printfn "\nresult:\n%s" rs
    | Error e ->
      printfn "Error: %s" e
  }

let jsonSerializerOptions =
  let jo = JsonSerializerOptions()
  jo.WriteIndented <- true
  jo

let div = String('-', 80)

let jsonToStr (jd:JsonDocument) = JsonSerializer.Serialize(jd.RootElement, jsonSerializerOptions)
  
let getIssue creds issue =
  async {
    match! JiraApi.getIssue creds BASE_URL issue with
    | Ok jd ->
      // printfn "\nIssue:\n%s\n" (jsonToStr jd) 
      return Issue.fromJson jd.RootElement
    | Error e ->
      printfn "Error: %s" e
      return Error e
  }

let printIssues creds startAt count =
  [1..count] 
  |> List.map(fun n -> sprintf "RCTFD-%i" (startAt + n))
  |> List.iter(fun id -> 
    match getIssue creds id |> Async.RunSynchronously with
    | Ok issue -> printfn "%s\nIssue: %s" div (string issue)
    | Error e -> printfn "%s" e     
  )

let printFields creds =
  match JiraApi.getFields creds BASE_URL |> Async.RunSynchronously with
  | Ok jd ->
    printfn "%s\nfields:\n%s" div (jsonToStr jd)
  | Error e -> printfn "%s" e

[<EntryPoint>]    
let main argv =
  printfn "FD-Jira. Experiments in Jira API driven utility.\nCopyright 2019-2020 Eric F. Vincent\n"
  Environment.GetEnvironmentVariable("JIRA_CREDS")
  |> Result.ofObj "No Creds Found!"
  |> Result.map (fun cr ->
      printIssues cr 4100 5
    )
  |> ignore


  0
