module Database

open LiteDB
open LiteDB.FSharp
open Types.Jira

let mapper = FSharpBsonMapper()
let db = new LiteDatabase("Filename=test.db;Mode=Exclusive", mapper)

let saveIssue (ctx:Prelude.Ctx) (issue:Issue) =
  try 
    ctx.log.Debug("Database.saveIssue|start|issue.id:{id}", issue.id)
    let items = db.GetCollection<Issue>("issues")
    items.Insert(issue) |> ignore
    ctx.log.Debug("Database.saveIssue|end|issue.id:{id}", issue.id)
    Ok ()
  with ex ->
    ctx.log.Error("Database.saveIssue|issue.id:{id}|{errorMessage}", issue.id, ex.Message)
    Error (string ex)