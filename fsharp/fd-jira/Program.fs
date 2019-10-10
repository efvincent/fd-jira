open System

open System.Text.Json
open Types.Jira
open efvJson
open DataBase

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

let issueFromJson (je:JsonElement) : Result<Types.Jira.TestIssue, string> =
  try
    let flds = getProp "fields" je
    {
      TestIssue.id = getPropStr "id" je
      key          = getPropStr "key" je
      summary      = getPropStr "summary" flds
      description  = getPropStrOpt "description" flds
      link         = getPropStr "self" je
      points       = getPropFloatOpt "customfield_10002" flds
    } |> Ok
  with
  | JsonParseExn jpe ->
    Error(sprintf  "Parse Error: %s" (string jpe))
  | ex ->
    Error(sprintf "Unexpected error: %s" (ex.ToString()))

let getIssue creds issue =
  async {
    match! JiraApi.getIssue creds BASE_URL issue with
    | Ok jd ->
      // let rs = 
      //   let opts = JsonSerializerOptions()
      //   opts.WriteIndented <- true
      //   JsonSerializer.Serialize(jd.RootElement, opts)
      // printfn "\nIssue:\n%s\n" rs 
      return issueFromJson jd.RootElement
    | Error e ->
      printfn "Error: %s" e
      return Error e
  }

[<EntryPoint>]    
let main argv =
  printfn "FD-Jira. Experiments in Jira API driven utility.\nCopyright 2019-2020 Eric F. Vincent\n"
  let creds = 
    match Environment.GetEnvironmentVariable("JIRA_CREDS") |> Option.ofObj with
    | Some s ->
      printfn "Creds found" 
      s
    | None -> "No Creds!"

  // getUpdatedItems creds |> Async.RunSynchronously
  match getIssue creds "RCTFD-4574" |> Async.RunSynchronously with
  | Ok issue -> printfn "Issue: %s" (string issue)
  | Error e -> printfn "%s" e

  0
