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

// TODO: Pass a fn to process one chunk at a time. Everything at once doesn't scale
let processChangedIssues 
  ctx baseUrl changedSince chunkSize = 
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
    let rec getBatches acc startAt chunkSize batchCount = async {
      ctx.log.Debug("jiraApi.processChangedIssues.getBatches|start|startAt:{start}|chunkSize:{chunkSize}|batchCount:{count}",
        startAt, chunkSize, batchCount)
      // get a page of changed issues
      match! getChangedIssues ctx baseUrl changedSince startAt chunkSize with
      | Ok batch ->
        // deserialize and add them to the accumulator
        let je = batch.RootElement
        let total = (getPropInt "total" je)
        let issues = deserIssues je |> List.ofSeq
        let acc' = issues :: acc
        // if fewer issues were returned than the max we specified then we're done
        let len = issues |> Seq.length
        ctx.log.Debug(
          "jiraApi.processChangedIssues.getBatches|end|startAt:{start}|chunkSize:{chunkSize}|len:{len}|total:{total}|batchCount:{count}",
          startAt, chunkSize, len, total, batchCount)
        if len < chunkSize then 
          return acc'
        // otherwise call for the next page
        else
          return! getBatches acc' (startAt + len) chunkSize (batchCount + 1)
      // if there's an error getting a page of changed issues, log and return what we have so far
      | Error e -> 
        ctx.log.Error("jiraApi.processChangedIssues.getBatches|startAt:{start}|chunkSize:{chunkSize}|batchCount:{count}|{err}",
          startAt, chunkSize, batchCount, e)
        return acc
    }

    let! batchesOfBatches = getBatches [] 0 chunkSize 1 
    let! itemGetResults = 
      batchesOfBatches 
      |> List.concat
      |> List.map (fun itemRef ->  getIssue ctx baseUrl itemRef.key)
      |> Async.Parallel
    let (items, upd) =
      itemGetResults
      |> Seq.fold (fun (acc,dt) res ->
          match res with
          | Ok jd -> 
            match JsonSerialization.Issue.fromJson jd.RootElement with
            | Ok issue ->
              if issue.updated > dt then (issue::acc, issue.updated) else (issue::acc,dt)
            | Error _ ->
              (acc,dt)    
          | Error _ -> 
            (acc,dt)
        ) ([], DateTimeOffset.MinValue)

    return {| items = items ; lastUpdate = upd |}
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