module Database

open System
open LiteDB
open LiteDB.FSharp
open LiteDB.FSharp.Extensions
open Types.Jira

let mapper = FSharpBsonMapper()
let db = new LiteDatabase("Filename=test.db;Mode=Exclusive", mapper)
let issues = db.GetCollection<Issue>("issues")
let systemState = 
  let col = db.GetCollection<SystemState>("sysState")
  col.EnsureIndex(fun s -> s.project) |> ignore
  col

let countIssues (ctx:Prelude.Ctx) =
  ctx.log.Debug("Database.countIssues|start")
  let c = issues.Count()
  ctx.log.Debug("Database.countIssues|end|count:{count}", c)
  c

let saveSystemState (ctx:Prelude.Ctx) (project:string) (lastUpdate:DateTimeOffset) =
  ctx.log.Debug("Database.saveSystemState|start|project:{project}|lastUpdate:{lastUpdate}",
    project, lastUpdate)

let tryGetSystemState (ctx:Prelude.Ctx) (project:string) =
  ctx.log.Debug("Database.getSystemState|start|project:{project}", project)
  match systemState.tryFindOne<SystemState> <@ fun ss -> ss.project = project @> with
  | Some ss -> 
    ctx.log.Debug("Database.getSystemState|end|project:{project}", project)
    Some ss
  | None -> 
    ctx.log.Information("Database.getSystemState|not-found|project:{project}", project)
    None

let saveIssue (ctx:Prelude.Ctx) (issue:Issue) =
  try 
    ctx.log.Debug("Database.saveIssue|start|issue.id:{id}", issue.id)
    issues.Upsert(issue) |> ignore
    ctx.log.Debug("Database.saveIssue|end|issue.id:{id}", issue.id)
    Ok ()
  with ex ->
    ctx.log.Error("Database.saveIssue|issue.id:{id}|{errorMessage}", issue.id, ex.Message)
    Error (string ex)

let tryGetIssue (ctx:Prelude.Ctx) (key:string) =
  ctx.log.Debug("Database.getIssue|start|key:{key}", key)
  match issues.tryFindOne<Issue> <@ fun issue -> issue.key = key @> with
  | Some issue -> 
    ctx.log.Debug("Database.getIssue|end|key:{key}", key)
    Some issue
  | None ->
    ctx.log.Error("Database.getIssue|not-found|key:{key}", key)
    None
  