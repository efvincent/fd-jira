module Types
open System

  module Jira =

    type Person = {
      key: string
      email: string
      name: string
    }

    [<RequireQualifiedAccess>]
    type IssueType =  
    | Unknown
    | Issue
    | Bug
    | Story 
    | Epic 
    | SubTask
    | Other of string 

    type Component =
    | Unknown
    | Archive
    | Mojo
    | Phoenix
    | Wolverine 
    | Ironman
    | Donatello
    | Product
    | Design
    | Platform
    | NativeTooling
    | Experience
    | Other of string 
   
    type Status = 
    | Unknown
    | Backlog
    | ReadyForWork
    | Active 
    | Done 
    | Deleted 
    | Other of string 

    /// A subset issue held as a property of sub-task issues indicating
    /// the parent of the sub-task
    type Parent = {
      key       : string
      id        : string
      summary   : string
      status    : Status
      issueType : IssueType
    }
    with 
      override this.ToString() =
        sprintf "%s %s %s - %s"
                this.key
                (string this.issueType)
                this.summary
                (string this.status)
                           
    type Issue = {
      key           : string
      id            : string
      epic          : string option
      summary       : string
      description   : string option
      resolution    : string option
      resolutionDate: DateTimeOffset option
      assignee      : Person option
      issueType     : IssueType
      parent        : Parent option
      status        : Status
      components    : Component seq
      link          : string
      points        : float option
      created       : DateTimeOffset
      updated       : DateTimeOffset
    }
    with
      override this.ToString() =
        let res = match this.resolutionDate with Some d -> (string d) | None -> ""
        sprintf "%s %s [%s]\nstatus: %s\nsummary: %s\nlink: %s\npoints: %f\ncreated: %A\nupdated: %A\nassignee: %s\nepic: %s\nparent: %s\n" 
                this.key
                (string this.issueType)
                (this.components |> Seq.map string |> (fun comps -> String.Join(",", comps)))
                (string this.status) this.summary 
                this.link
                (this.points |> Option.defaultValue 0.)
                this.created this.updated
                (match this.assignee with Some a -> a.name | None -> "unassigned")
                (match this.epic with Some e -> e | None -> "[no epic]")
                (match this.parent with Some p -> (string p) | None -> "[not a subtask]")


