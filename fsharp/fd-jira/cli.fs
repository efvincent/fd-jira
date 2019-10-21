module Cli

  open CommandLine

  [<Verb("api", HelpText="Pass the REST API directly to Jira. GET verb only.")>]
  type PassThruOpts = {
    [<Value(0, MetaName="REST-URI", HelpText="The URI to pass to Jira, excluding the base portion. Ex: \"/rest/api/2/field\"")>]
    query: string
  }

  [<Verb("range", HelpText="Work with a range of Jira tickets")>]
  type RangeOpts = {
    [<Value(0, MetaName="StartAt", Required=true, HelpText="Ticket number to start. Integer. Ex: the 1234 in RCTFD-1234")>]
    startAt: int
    [<Value(1, MetaName="Count", Required=true, HelpText="Number of tickets to get")>]
    count: int
  }

  [<RequireQualifiedAccess>]
  type Opts =
  | PassThru of PassThruOpts
  | Range of RangeOpts
  | Unknown