open System
open LiteDB
open System.Text.Json
open Types.Jira
open efvJson

[<Literal>]
let BASE_URL = "https://jira.walmart.com/rest/api/2"

type Stock (ticker, price) = 
  member val Id = 0 with get,set
  member val Ticker = ticker with get,set
  member val Price = price with get,set
  member val Comment = "" with get,set
  override this.ToString () =
    sprintf "[%i] %s @ %f : %s" this.Id this.Ticker this.Price this.Comment


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

let issueFromJson (je:JsonElement) : Types.Jira.TestIssue option =
  try
    let flds = getProp "fields" je
    {
      TestIssue.id = getPropStr "id" je
      key = getPropStr "key" je
      summary = getPropStr "summary" flds
    } |> Some
  with
  | JsonParseExn jpe ->
    printfn "Parse Error: %s" (string jpe)
    None
  | ex ->
    printfn "Unexpected error: %s" (ex.ToString())
    None

let getIssue creds issue =
  async {
    match! JiraApi.getIssue creds BASE_URL issue with
    | Ok jd ->
      let rs = 
        let opts = JsonSerializerOptions()
        opts.WriteIndented <- true
        JsonSerializer.Serialize(jd.RootElement, opts)
      printfn "\nIssue:\n%s\n" rs 
      let testIssue = issueFromJson jd.RootElement
      printfn "Issue:%s" (string testIssue)
    | Error e ->
      printfn "Error: %s" e
  }

[<EntryPoint>]    
let main argv =

  let creds = 
    match Environment.GetEnvironmentVariable("JIRA_CREDS") |> Option.ofObj with
    | Some s -> s
    | None -> ""

  // getUpdatedItems creds |> Async.RunSynchronously
  getIssue creds "RCTFD-4574" |> Async.RunSynchronously

  0
