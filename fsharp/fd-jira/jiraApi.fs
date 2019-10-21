module JiraApi

open System
open System.Net.Http
open System.Text.Json
open Prelude

[<Literal>]
let PROJECT_CODE = "RCTFD"

// use one HttpClient for all calls
let httpClient = new HttpClient()

let makeJiraCall ctx url =
  async {
    let req = new HttpRequestMessage()
    req.Method <- HttpMethod.Get
    req.RequestUri <- Uri url  
    req.Headers.Authorization <-
      let bytes = System.Text.UTF8Encoding.UTF8.GetBytes ctx.creds
      let encodedCreds = System.Convert.ToBase64String bytes
      Headers.AuthenticationHeaderValue("Basic", encodedCreds)
    ctx.log.Information("makeJiraCall|start|method:{method}|url:{url}", req.Method, req.RequestUri)
    let! rsp = httpClient.SendAsync req |> Async.AwaitTask
    if rsp.IsSuccessStatusCode then 
      let! content = rsp.Content.ReadAsStreamAsync() |> Async.AwaitTask
      let! jd =  JsonDocument.ParseAsync(content) |> Async.AwaitTask
      return Ok(jd)
    else 
      let status = LanguagePrimitives.EnumToValue rsp.StatusCode
      ctx.log.Error ("makeJiraCall|Error|method:{method}|status:{statusCode}|code:{status}|reason:{statusReason}|url:{url}", 
        req.Method, rsp.StatusCode, status, rsp.ReasonPhrase, url)
      return Error (sprintf "(%i) %A; Reason: \"%s\"" status rsp.StatusCode rsp.ReasonPhrase)
  }

let makeUpdateQuery project (updatedSince:DateTimeOffset) =
  let dt = updatedSince.ToString("yyyy-MM-dd HH:mm")
  let query = sprintf "project=%s AND updatedDate >= \"%s\"" project dt
  Uri.EscapeDataString query
  
let getChangedIssues ctx baseUrl startAt =
  async {
    let query = makeUpdateQuery PROJECT_CODE DateTimeOffset.MinValue
    let url = sprintf "%s/search?jql=%s&expand=name&maxResults=100&fields=updated&startAt=%i"
                baseUrl query startAt
    printfn "url: %s\n" url
    return! makeJiraCall ctx url
  }

let getIssue ctx baseUrl issue =
  async {
    ctx.log.Debug(sprintf "getIssue|start|\"%s\"" issue)
    let url = sprintf "%s/issue/%s%s%s%s"
                        baseUrl 
                        issue
                        "?fields=assignee,status,summary,description,created,updated,resolutiondate,"
                        "issuetype,components,priority,resolution,"
                        "customfield_10002,subtasks,customfield_10007,parent"
    let! result = makeJiraCall ctx url
    match result with 
    | Ok _    -> ctx.log.Debug(sprintf "getIssue|end|\"%s\"|Success" issue)
    | Error _ -> ctx.log.Debug(sprintf "getIssue|end|\"%s\"|Fail" issue)
    return result
  } 

let getFields ctx baseUrl =
  async {
    let url = sprintf "%s/field" baseUrl
    return! makeJiraCall ctx url
  }