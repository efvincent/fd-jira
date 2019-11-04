module Database

open System
open LiteDB
open LiteDB.FSharp
open LiteDB.FSharp.Extensions
open Types.Jira

let private mapper = FSharpBsonMapper()
let private dbFile = 
  let dir =
    let targetDir =
      IO.Path.Combine ( 
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        ".fdjira")
    if not (IO.Directory.Exists(targetDir)) 
    then IO.Directory.CreateDirectory(targetDir) |> ignore
    targetDir
  IO.Path.Combine(dir, "cache.db")
let private db = new LiteDatabase((sprintf "Filename=%s;Mode=Exclusive" dbFile), mapper)
let private issues = db.GetCollection<Issue>("issues")  
let private systemState = 
  let col = db.GetCollection<SystemState>("sysState")
  col.EnsureIndex(fun s -> s.project) |> ignore
  col

let logDbInfo (ctx:Prelude.Ctx) =
  ctx.log.Information("Database.logDbInfo|mode=Exclusive|filename={dbFile}", dbFile)

let countIssues (ctx:Prelude.Ctx) =
  ctx.log.Debug("Database.countIssues|start")
  let c = issues.Count()
  ctx.log.Debug("Database.countIssues|end|count:{count}", c)
  c

let tryGetSystemState (ctx:Prelude.Ctx) (project:string) =
  ctx.log.Debug("Database.getSystemState|start|project:{project}", project)
  match systemState.tryFindOne<SystemState> <@ fun ss -> ss.project = project @> with
  | Some ss -> 
    ctx.log.Debug("Database.getSystemState|end|project:{project}", project)
    Some ss
  | None -> 
    ctx.log.Information("Database.getSystemState|not-found|project:{project}", project)
    None

let saveSystemState (ctx:Prelude.Ctx) (project:string) (lastUpdate:DateTime) =
  ctx.log.Debug("Database.saveSystemState|start|project:{project}|lastUpdate:{lastUpdate}",
    project, lastUpdate)
  try 
    match tryGetSystemState ctx project with
    | Some ss ->
      ctx.log.Debug("Database.saveSystemState|Existing system state found; updating")
      systemState.Update {ss with issuesUpdated = lastUpdate } |> ignore
    | None ->
      ctx.log.Debug(
        "Database.saveSystemState|FirstSave|System state for {project} not found, creating new system state", 
        project)       
      systemState.Upsert { id = ""; project = project; issuesUpdated = lastUpdate } |> ignore
    ctx.log.Debug("Database.saveSystemState|end|project:{project}", project)
    Ok ()
  with ex ->
    ctx.log.Error("Database.saveSystemState|project:{project}|lastUpdate:{lastUpdate}|{err}", 
      project, lastUpdate, ex.Message)
    Error (string ex)

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

let sampleQuery (ctx:Prelude.Ctx) =
  ctx.log.Debug("Database.sampleQuery|start")
  let pred =
    Query.And(
        Query.EQ("status", BsonValue("Active")),
        Query.EQ("components[*]", BsonValue("Phoenix")),
        // Query.GTE("updated", BsonValue(DateTime.Parse("2019-10-28T00:00")))
        Query.Between(
          "updated", 
          BsonValue(DateTime.Parse("10/20/2019")), 
          BsonValue(DateTime.Parse("10/25/2019"))
        )
    ) 
    
  let cnt = issues.Count(pred)
  let recs = issues.Find(pred, limit=100)

  ctx.log.Debug("Database.sampleQuery|end|count:{count}", cnt)
  (cnt, recs)


