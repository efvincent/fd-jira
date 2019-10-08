open System
open System.IO
open LiteDB
open System.Text.Json

type Stock (ticker, price) = 
  member val Id = 0 with get,set
  member val Ticker = ticker with get,set
  member val Price = price with get,set
  member val Comment = "" with get,set
  override this.ToString () =
    sprintf "[%i] %s @ %f : %s" this.Id this.Ticker this.Price this.Comment

[<EntryPoint>]    
let main argv =

  let creds = 
    match Environment.GetEnvironmentVariable("JIRA_CREDS") |> Option.ofObj with
    | Some s -> 
        let parts = s.Split(':')
        if parts.Length = 2 then 
            Ok (parts.[0], parts.[1]) 
        else 
            Error("Invalid format for JIRA_CREDS")
    | None ->
        Error("Indefined environment variable JIRA_CREDS")

  match creds with
  | Ok(un,pw) -> printfn "creds: %s : %s" un pw
  | Error e -> printfn "Error: %s" e

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

  0
