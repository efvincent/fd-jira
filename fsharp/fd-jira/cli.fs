module Cli

  open CommandLine

  [<Verb("info", HelpText="Application Information")>]
  type BasicOpts = {
    [<Option('v', "Verbose", HelpText="Display the version of JiraTool")>]
    version: bool
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
  | Basic of BasicOpts
  | Range of RangeOpts
  | Unknown