module JsonSerialization 

open Json
open Types.Jira
open System.Text.Json
open Microsoft.FSharp.Core

  let private _wrapWithTry fn =
    try
      fn ()
    with
    | JsonParseException jpe ->
      Error(sprintf  "Parse Error: %s" (string jpe))
    | ex ->
      Error(sprintf "Unexpected error: %s" (ex.ToString()))    

    module IssueType =
      let fromJson je =
        _wrapWithTry (fun () -> 
          match (getPropStr "name" je).ToLower()  with
          | "issue"    -> Ok IssueType.Issue
          | "epic"     -> Ok IssueType.Epic
          | "story"    -> Ok IssueType.Story
          | "subtask"
          | "sub-task" -> Ok IssueType.SubTask
          | other      -> Ok (IssueType.Other other)
        )

    module Component =
      let fromJson je =
        _wrapWithTry (fun () -> 
          match (getPropStr "name" je).ToLower() with
          | "mojo"      -> Ok Mojo
          | "phoenix"   -> Ok Phoenix
          | "wolverine" -> Ok Wolverine
          | "design"    -> Ok Design
          | "ironman"   -> Ok Ironman
          | other       -> Ok (Component.Other other)
        )

    module Status =
      let fromJson je =
        _wrapWithTry (fun () ->
          match (getPropStr "name" je).ToLower() with
          | "work in progress"
          | "active"         -> Ok Active
          | "backlog"        -> Ok Backlog
          | "deleted"        -> Ok Deleted
          | "done"           -> Ok Done
          | "ready-for-work" -> Ok ReadyForWork
          | other            -> Ok (Other other)
        )

    module Issue =
      let fromJson je =
        _wrapWithTry (fun () ->
          let flds = getProp "fields" je
          let comps = 
            getProp "components" flds
            |> getArray
            |> Seq.map Component.fromJson
            |> Seq.concatResult
            |> Result.orFailWith
          {
            Issue.id     = getPropStr "id" je
            key          = getPropStr "key" je
            summary      = getPropStr "summary" flds
            description  = getPropStrOpt "description" flds
            issueType    = IssueType.fromJson (getProp "issuetype" flds) |> Result.orFailWith
            status       = Status.fromJson (getProp "status" flds) |> Result.orFailWith
            components   = comps
            link         = getPropStr "self" je
            points       = getPropFloatOpt "customfield_10002" flds
          } |> Ok)
