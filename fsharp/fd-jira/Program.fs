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

let getIssue creds issue =
  async {
    match! JiraApi.getIssue creds BASE_URL issue with
    | Ok jd ->
      // let rs = 
      //   let opts = JsonSerializerOptions()
      //   opts.WriteIndented <- true
      //   JsonSerializer.Serialize(jd.RootElement, opts)
      // printfn "\nIssue:\n%s\n" rs 
      return Issue.fromJson jd.RootElement
    | Error e ->
      printfn "Error: %s" e
      return Error e
  }

[<EntryPoint>]    
let main argv =
  printfn "FD-Jira. Experiments in Jira API driven utility.\nCopyright 2019-2020 Eric F. Vincent\n"
  let div = String('-', 80)
  Environment.GetEnvironmentVariable("JIRA_CREDS")
  |> Result.ofObj "No Creds Found!"
  |> Result.map (fun cr ->
      [1..20] 
      |> List.map(fun n -> sprintf "RCTFD-%i" (3840 + n))
      |> List.iter(fun id -> 
        match getIssue cr id |> Async.RunSynchronously with
        | Ok issue -> printfn "%s\nIssue: %s" div (string issue)
        | Error e -> printfn "%s" e     
      )
    )
  |> ignore

  0
