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
    | Story 
    | Epic 
    | SubTask
    | Other of string 

    type Component =
    | Unknown
    | Mojo
    | Phoenix
    | Wolverine 
    | Ironman
    | Product
    | Design
    | Other of string 
   
    type Status = 
    | Unknown
    | Backlog
    | ReadyForWork
    | Active 
    | Done 
    | Deleted 
    | Other of string 

    type Issue = {
      key           : string
      id            : string
      summary       : string
      description   : string option
      resolution    : string option
      resolutionDate: DateTimeOffset option
      issueType     : IssueType
      status        : Status
      components    : Component seq
      link          : string
      points        : float option
      created       : DateTimeOffset
    }
    with
      override this.ToString() =
        let res = match this.resolutionDate with Some d -> (string d) | None -> ""
        sprintf "%s %s [%s]\nstatus: %s\nsummary: %s\nlink: %s\npoints: %f\ncreated: %A\nresolved: %s\nresolution: %s\n" 
                this.key
                (string this.issueType)
                (this.components |> Seq.map string |> (fun comps -> String.Join(",", comps)))
                (string this.status) this.summary 
                this.link
                (this.points |> Option.defaultValue 0.)
                this.created
                res
                (this.resolution |> Option.defaultValue "[not yet resolved]")

