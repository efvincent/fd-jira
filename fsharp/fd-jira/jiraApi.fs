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

let makeUpdateQuery project (updatedSince:DateTime) =
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
  
let getIssue ctx baseUrl issue =
  async {
    ctx.log.Debug(sprintf "getIssue|start|\"%s\"" issue)
    let url = sprintf "%s/rest/agile/1.0/issue/%s%s%s%s"
                        baseUrl 
                        issue
                        "?fields=assignee,status,summary,description,created,updated,resolutiondate,"
                        "issuetype,components,priority,resolution,sprint,"
                        "customfield_10002,subtasks,customfield_10007,parent,labels"
    let! result = makeJiraCall ctx url
    match result with 
    | Ok _    -> ctx.log.Debug(sprintf "getIssue|end|\"%s\"|Success" issue)
    | Error _ -> ctx.log.Debug(sprintf "getIssue|end|\"%s\"|Fail" issue)
    return result
  } 

let processChangedIssues 
  ctx baseUrl changedSince chunkSize (procFn:string -> Async<unit>) (batchProgress: int -> int -> int -> unit) = 
  async {
    /// Deserialize sequence of found issues into anon record
    let deserIssues je =
      (getArray (getProp "issues" je))
      |> Seq.map (fun itemJe -> 
        {|
          id = (getPropStr "id" itemJe)
          key = (getPropStr "key" itemJe)
          updated = (getPropDateTime "updated" (getProp "fields" itemJe))
        |})

    /// Recursively gets pages of changed issue records until no more are returned
    let rec getBatches 
          (result:{| count: int; updated: DateTime |}) 
          startAt chunkSize batchCount = async {
      ctx.log.Debug("jiraApi.processChangedIssues.getBatches|start|startAt:{start}|chunkSize:{chunkSize}|batchCount:{count}",
        startAt, chunkSize, batchCount)
      // get a page of changed issues
      match! getChangedIssues ctx baseUrl changedSince startAt chunkSize with
      | Ok batch ->
        let je = batch.RootElement
        let total = (getPropInt "total" je)
        let issues = deserIssues je |> List.ofSeq
        // find the latest update date from the list
        let upd' = 
          let lastFromBatch =
            issues
            |> List.map(fun i -> i.updated)
            |> List.max
          if lastFromBatch > result.updated then lastFromBatch else result.updated
        // if fewer issues were returned than the max we specified then we're done
        let len = issues |> Seq.length
        ctx.log.Debug(
          "jiraApi.processChangedIssues.getBatches|end|startAt:{start}|chunkSize:{chunkSize}|len:{len}|total:{total}|batchCount:{count}",
          startAt, chunkSize, len, total, batchCount)

        // report progress
        batchProgress startAt len total
        
        // process the issues
        let! _ = 
          issues 
          |> List.map(fun item -> procFn item.key)
          |> Async.Parallel        

        let result' = {|count = result.count + len; updated = upd' |}
        if len >= chunkSize then 
          return! getBatches result' (startAt + len) chunkSize (batchCount + 1)
        else
          return result'
      | Error e -> 
        ctx.log.Error("jiraApi.processChangedIssues.getBatches|startAt:{start}|chunkSize:{chunkSize}|batchCount:{count}|{err}",
          startAt, chunkSize, batchCount, e)
        return result
    }

    return! getBatches {|count=0; updated=DateTime.MinValue|} 0 chunkSize 1 
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