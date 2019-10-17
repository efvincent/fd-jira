module JiraApi

open System
open System.Net.Http
open System.Text.Json

[<Literal>]
let PROJECT_CODE = "RCTFD"

// use one HttpClient for all calls
let httpClient = new HttpClient()

let makeJiraCall (creds:string) url =
  async {
    let req = new HttpRequestMessage()
    req.Method <- HttpMethod.Get
    req.RequestUri <- Uri url  
    req.Headers.Authorization <-
      let bytes = System.Text.UTF8Encoding.UTF8.GetBytes creds
      let encodedCreds = System.Convert.ToBase64String bytes
      Headers.AuthenticationHeaderValue("Basic", encodedCreds)
    let! rsp = httpClient.SendAsync req |> Async.AwaitTask
    if rsp.IsSuccessStatusCode then 
      let! content = rsp.Content.ReadAsStreamAsync() |> Async.AwaitTask
      let! jd =  JsonDocument.ParseAsync(content) |> Async.AwaitTask
      return Ok(jd)
    else 
      return Error (sprintf "Error: HTTP Response code: %A" rsp.StatusCode)
  }

let makeUpdateQuery project (updatedSince:DateTimeOffset) =
  let dt = updatedSince.ToString("yyyy-MM-dd HH:mm")
  let query = sprintf "project=%s AND updatedDate >= \"%s\"" project dt
  Uri.EscapeDataString query
  
let getChangedIssues creds baseUrl startAt =
  async {
    let query = makeUpdateQuery PROJECT_CODE DateTimeOffset.MinValue
    let url = sprintf "%s/search?jql=%s&expand=name&maxResults=100&fields=updated&startAt=%i"
                baseUrl query startAt
    printfn "url: %s\n" url
    return! makeJiraCall creds url
  }

let getIssue creds baseUrl issue =
  async {
    let url = sprintf "%s/issue/%s?fields=assignee,status,summary,description,created,updated,resolutiondate,issuetype,components,priority,resolution,customfield_10002,subtasks,customfield_10007,parent"
                        baseUrl issue
    return! makeJiraCall creds url
  } 

let getFields creds baseUrl =
  async {
    let url = sprintf "%s/field" baseUrl
    return! makeJiraCall creds url
  }