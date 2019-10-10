module DataBase
open LiteDB

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