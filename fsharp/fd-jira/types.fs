module Types
open System

    module Jira =

        type Person = {
            key: string
            email: string
            name: string
        }

        type IssueType =  
        | Unknown
        | Issue
        | Story 
        | Epic 
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
            id            : int
            summary       : string
            description   : string
            issueType     : IssueType
            points        : float
            components    : Component []
            status        : Status
            resolutionDate: DateTimeOffset
            created       : DateTimeOffset
            assignee      : Person option
            updated       : DateTimeOffset
        }

        type TestIssue = {
            key        : string
            id         : string
            summary    : string
            description: string option
            link       : string
            points     : float option
        }
        with
            override this.ToString() =
                sprintf "(%s) %s\nsummary: %s\ndescription: %s\nlink: %s\npoints: %f" 
                        this.id this.key this.summary 
                        (this.description |> Option.defaultValue "[none]")
                        this.link
                        (this.points |> Option.defaultValue 0.)