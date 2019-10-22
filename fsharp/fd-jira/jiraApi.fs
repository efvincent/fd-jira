module JiraApi

open System
open System.Net.Http
open System.Text.Json
open Prelude
open Json

[<Literal>] 
let PROJECT_CODE = "RCTFD"

[<Literal>] 
let MAX_HTTP_CONNECTIONS = 20

let jdOpts = JsonDocumentOptions(AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Skip)
  
// use one HttpClient for all calls
let httpClient =
  let handler = new HttpClientHandler()
  handler.MaxConnectionsPerServer <- MAX_HTTP_CONNECTIONS
  let client = new HttpClient(handler)
  client

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
      let! jd =  JsonDocument.ParseAsync(content, jdOpts) |> Async.AwaitTask
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
  
let getChangedIssues ctx baseUrl changedSince startAt maxCount =
  async {
    let query = makeUpdateQuery PROJECT_CODE changedSince
    let url = sprintf "%s/rest/api/2/search?jql=%s&expand=name&maxResults=%i&fields=updated&startAt=%i"
                baseUrl query maxCount startAt
    return! makeJiraCall ctx url
  }

let processChangedIssues ctx baseUrl changedSince chunkSize =
  async {
    // let tot = 101
    // let step = 10
    // let max = if tot % step = 0 then tot else tot + step
    // let s = seq { for i in step-1 .. 10 .. max do yield i }
    // s |> Seq.iter (fun n -> printfn "%i" n)
    // get first chunk, get the max number from that
    // use ^ to do generate a series of parameters
    // map those into a sequence of async workflows to do the HTTP call to get chunks
    // collect those into single list, map to the keys
    // map those into a sequence of async workflows to do the HTTP call to get items and save them to the db
    let getIssues je =
      (getArray (getProp "issues" je))
      |> Seq.map (fun itemJe -> 
        {|
          id = (getPropStr "id" itemJe)
          key = (getPropStr "key" itemJe)
          updated = (getPropDateTime "updated" (getProp "fields" itemJe))
        |})
    match! getChangedIssues ctx baseUrl changedSince 0 chunkSize with
    | Ok firstBatch -> 
      let je = firstBatch.RootElement
      let total = (getInt (getProp "total" je))
      let firstBatch = getIssues je
      let total = if total % chunkSize = 0 then total else total + chunkSize  // extra call if not exact factor of chunkSize
      
      ()
    | Error e ->
      ()
    return 0
  }

let getIssue ctx baseUrl issue =
  async {
    ctx.log.Debug(sprintf "getIssue|start|\"%s\"" issue)
    let url = sprintf "%s/rest/agile/1.0/issue/%s%s%s%s"
                        baseUrl 
                        issue
                        "?fields=assignee,status,summary,description,created,updated,resolutiondate,"
                        "issuetype,components,priority,resolution,sprint,"
                        "customfield_10002,subtasks,customfield_10007,parent"
    let! result = makeJiraCall ctx url
    match result with 
    | Ok _    -> ctx.log.Debug(sprintf "getIssue|end|\"%s\"|Success" issue)
    | Error _ -> ctx.log.Debug(sprintf "getIssue|end|\"%s\"|Fail" issue)
    return result
  } 

let getFields ctx baseUrl =
  async {
    let url = sprintf "%s/rest/api/2/field" baseUrl
    return! makeJiraCall ctx url
  }

let passThru ctx baseUrl query =
  async {
    ctx.log.Debug(sprintf "passThru|start|\"%s\"" query)
    let url = sprintf "%s%s" baseUrl (if query.StartsWith("/") then query else (sprintf "/%s" query))
    let! result = makeJiraCall ctx url
    match result with 
    | Ok _    -> ctx.log.Debug(sprintf "passThru|end|\"%s\"|Success" query)
    | Error _ -> ctx.log.Debug(sprintf "passThru|end|\"%s\"|Fail" query)
    return result
  }