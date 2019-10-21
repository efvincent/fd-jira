module JsonSerialization 

  open Json
  open Types.Jira
  open Microsoft.FSharp.Core

  let private _wrapWithTry fn =
    try
      fn ()
    with
    | JsonParseException jpe ->
      Error(sprintf  "Parse Error: %s" (string jpe))
    | ex ->
      Error(sprintf "Unexpected error: %s" (ex.ToString()))    

  module PersonType =
    let fromJson je =
      _wrapWithTry (fun () ->
        {
          email = getPropStr "emailAddress" je
          key = getPropStr "key" je
          name = getPropStr "displayName" je
        } |> Ok
      )

  module IssueType =
    let fromJson je =
      _wrapWithTry (fun () -> 
        match (getPropStr "name" je).ToLower()  with
        | "issue"    -> Ok IssueType.Issue
        | "epic"     -> Ok IssueType.Epic
        | "story"    -> Ok IssueType.Story
        | "bug"      -> Ok IssueType.Bug
        | "subtask"
        | "sub-task" -> Ok IssueType.SubTask
        | other      -> Ok (IssueType.Other other)
      )

  module Component =
    let fromJson je =
      _wrapWithTry (fun () -> 
        match (getPropStr "name" je).ToLower() with
        | "mojo"           -> Ok Mojo
        | "phoenix"        -> Ok Phoenix
        | "wolverine"      -> Ok Wolverine
        | "design"         -> Ok Design
        | "ironman"        -> Ok Ironman
        | "donatello"      -> Ok Donatello
        | "product"        -> Ok Product
        | "platform"
        | "platform q2"    
        | "platform q3"    -> Ok Platform
        | "experience"     -> Ok Experience
        | "native tooling" -> Ok NativeTooling
        | "archive"        -> Ok Archive
        | other            -> Ok (Component.Other other)
      )

  module Status =
    let fromJson je =
      _wrapWithTry (fun () ->
        match (getPropStr "name" je).ToLower() with
        | "work in progress"
        | "active"           -> Ok Active
        | "backlog"          -> Ok Backlog
        | "deleted"          -> Ok Deleted
        | "done"             -> Ok Done
        | "ready-for-work"   -> Ok ReadyForWork
        | "ready to start"   -> Ok ReadyToStart
        | "ready for review" -> Ok ReadyForReview
        | other              -> Ok (Other other)
      )

  module Parent =
    let fromJson je =
      _wrapWithTry (fun () ->
        let flds  = getProp "fields" je
        {
          Parent.id = getPropStr "id" je
          key       = getPropStr "key" je
          summary   = getPropStr "summary" flds
          status    = Status.fromJson (getProp "status" flds) |> Result.orFailWith
          issueType = IssueType.fromJson (getProp "issuetype" flds) |> Result.orFailWith
        } |> Ok
      )

  module Sprint =
    let fromJson je =
      _wrapWithTry (fun () ->
        {
          Sprint.id     = getPropStr "id" je
          name          = getPropStr "name" je
          goal          = getPropStr "goal" je 
          state         = getPropStr "state" je
          startDate     = getPropDateTime "startDate" je
          endDate       = getPropDateTime "endDate" je
          originBoardId = getPropInt "originBoardId" je
        } |> Ok
      )

  module Issue =
    let fromJson je =
      _wrapWithTry (fun () ->
        let flds  = getProp "fields" je
        let res   = getPropOpt "resolution" flds
        let sprint = 
          getPropOpt "sprint" flds 
          |> Option.bind (fun je ->
            match Sprint.fromJson je with 
            | Ok sprint -> Some sprint
            | Error _ -> None
          )
        let assignee = 
          getPropOpt "assignee" flds
          |> Option.bind(fun je -> 
            match PersonType.fromJson je with
            | Ok p -> Some p
            | Error _ ->
              None          
          )
        let comps = 
          getProp "components" flds
          |> getArray
          |> Seq.map Component.fromJson
          |> Seq.concatResult
          |> Result.orFailWith
          |> Set.ofSeq
        {
          Issue.id       = getPropStr "id" je
          key            = getPropStr "key" je
          epic           = getPropStrOpt "customfield_10007" flds
          summary        = getPropStr "summary" flds
          description    = getPropStrOpt "description" flds
          resolution     = res |> Option.bind (getPropStrOpt "name") 
          resolutionDate = getPropDateTimeOpt "resolutiondate" flds
          issueType      = IssueType.fromJson (getProp "issuetype" flds) |> Result.orFailWith
          sprint         = sprint 
          status         = Status.fromJson (getProp "status" flds) |> Result.orFailWith
          components     = comps
          assignee       = assignee
          link           = getPropStr "self" je
          points         = getPropFloatOpt "customfield_10002" flds
          created        = getPropDateTime "created" flds
          updated        = getPropDateTime "updated" flds
          parent         = match getPropOpt "parent" flds with 
                           | Some jd -> 
                              match Parent.fromJson jd with 
                              | Ok parent -> Some parent
                              | Error e -> printf "Error deserializing parent: %s" e; None
                           | None -> None
        } |> Ok)
