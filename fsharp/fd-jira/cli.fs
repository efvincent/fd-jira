module Cli

  open CommandLine
  open System

  [<Verb("count", HelpText="Count element")>]
  type CountOpts = {
    [<Value(0, MetaName="target", HelpText="What to count. Possible values: \"issues\"")>]
    target: string
  }

  [<Verb("sync", HelpText="Sync issues for project")>]
  type SyncOpts =  {
    [<Value(0, MetaName="since", HelpText="Sync everything changed after this date")>]
    lastUpdate: DateTime option
  }

  [<Verb("api", HelpText="Pass the REST API directly to Jira. GET verb only.")>]
  type PassThruOpts = {
    [<Value(0, MetaName="REST-URI", HelpText="The URI to pass to Jira, excluding the base portion. Ex: \"/rest/api/2/field\"")>]
    query: string
  }

  [<Verb("get", HelpText="Get one or more issues from local database")>]
  type GetOpts = {
    [<Value(0, MetaName="key", HelpText="Key of issue to get from the database")>]
    key: string
  }

  [<Verb("bulk", HelpText="Bulk get items changed since a particular date")>]
  type BulkOpts = {
    [<Value(0, MetaName="startAt", HelpText="Start at this record number (for paging)")>]
    startAt: int
    [<Value(1, MetaName="maxCount", HelpText="Maximum number of records to return (for paging)")>]
    maxCount: int
    [<Value(2, MetaName="date", HelpText="Get items changed after this date")>]
    changedSince: DateTime
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
  | Count    of CountOpts
  | PassThru of PassThruOpts
  | Sync     of SyncOpts
  | Get      of GetOpts
  | Bulk     of BulkOpts
  | Range    of RangeOpts
  | Unknown

  /// Return the selected CLI options if any
  let getCliOpts (ctx:Prelude.Ctx) (argv:string []) =
    //let argv = ["bulk"; "0"; "100"; "01/01/2018"]
    if argv |> Seq.length > 0 then 
      ctx.log.Information("getCliOpts|args|{0}", (String.Join(' ', argv)))
    else
      ctx.log.Information("getCliOpts|No args passed") 

    let cliResult =
      CommandLine
        .Parser
        .Default
        .ParseArguments(argv, 
          typeof<CountOpts>,
          typeof<PassThruOpts>,
          typeof<SyncOpts>,
          typeof<RangeOpts>,
          typeof<GetOpts>,
          typeof<BulkOpts>)
    let opts =    
      match cliResult with
      | :? CommandLine.Parsed<obj> as verb ->
        match verb.Value with
        | :? CountOpts    as opts -> Ok <| Opts.Count opts
        | :? PassThruOpts as opts -> Ok <| Opts.PassThru opts
        | :? GetOpts      as opts -> Ok <| Opts.Get opts
        | :? SyncOpts     as opts -> Ok <| Opts.Sync opts
        | :? BulkOpts     as opts -> Ok <| Opts.Bulk opts
        | :? RangeOpts    as opts -> Ok <| Opts.Range opts
        | t -> 
          ctx.log.Error("args|Missing parse case for verb type|{0}", (t.GetType().Name))
          Result.Error (sprintf "Missinc parse case for verb type: %s" (t.GetType().Name))
      | :? CommandLine.NotParsed<obj> as np -> 
        ctx.log.Warning("args|Not parsed|{@0}", np)
        Result.Error (
          sprintf "Not parsed: %s" 
            (np.Errors 
            |> Seq.map(fun e -> string e.Tag) 
            |> (fun errs -> String.Join(',', errs))))
      | _ -> 
        let msg = "Unexpected CLI parser response"
        ctx.log.Fatal("args|{0}", msg)
        Result.Error msg
    opts