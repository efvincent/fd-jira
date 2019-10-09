open System
open System.IO
open LiteDB
open System.Text.Json
open System.Net.Http

// use one HttpClient for all calls
let httpClient = new HttpClient()

[<Literal>]
let BASE_URL = "https://jira.walmart.com/rest/api/2"

type Stock (ticker, price) = 
  member val Id = 0 with get,set
  member val Ticker = ticker with get,set
  member val Price = price with get,set
  member val Comment = "" with get,set
  override this.ToString () =
    sprintf "[%i] %s @ %f : %s" this.Id this.Ticker this.Price this.Comment

let makeJiraCall (creds:string) url =
  async {
    let req = new HttpRequestMessage()
    req.Method <- HttpMethod.Get
    req.RequestUri <- new Uri(url)  
    req.Headers.Authorization <-
      let bytes = System.Text.UTF8Encoding.UTF8.GetBytes creds
      let encodedCreds = System.Convert.ToBase64String bytes
      new Headers.AuthenticationHeaderValue("Basic", encodedCreds)
    let! rsp = httpClient.SendAsync req |> Async.AwaitTask
    if rsp.IsSuccessStatusCode then 
      return! rsp.Content.ReadAsStringAsync() |> Async.AwaitTask
    else 
      return ""
  }

let makeQuery project (updatedSince:DateTimeOffset) =
  let dt = updatedSince.ToString("yyyy-MM-dd HH:mm")
  let query = sprintf "project=%s AND updatedDate >= \"%s\"" project dt
  Uri.EscapeDataString query
  
let getChangedIssues creds baseUrl startAt =
  async {
    let query = makeQuery "RCTFD" DateTimeOffset.MinValue
    let url = sprintf "%s/search?jql=%s&expand=name&maxResults=100&fields=updated&startAt=%i"
                baseUrl query startAt
    return! makeJiraCall creds url
  }

let doDbStuff () =
  use db = new LiteDatabase("mydata.db")
  let stocks = db.GetCollection<Stock>()
  let s = Stock("WMT", 108.36)
  s.Id <- 1
  let res = stocks.Upsert s
  printfn "insert result: %A" res
  s.Comment <- "This is a sample record in the document db"
  match stocks.Update s with 
  | true -> printfn "success!"
  | _ -> printfn "update failed?"

  stocks.EnsureIndex(fun x -> x.Comment) |> ignore
  let results = stocks.Find(fun x -> x.Comment.StartsWith("This"))
  printfn "%A" results


[<EntryPoint>]    
let main argv =

  let creds = 
    match Environment.GetEnvironmentVariable("JIRA_CREDS") |> Option.ofObj with
    | Some s -> s
    | None -> ""

  let result = getChangedIssues creds BASE_URL 0 |> Async.RunSynchronously
  printfn "result:\n%s" result
  
  0
