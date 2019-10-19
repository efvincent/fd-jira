module Prelude

  open Serilog
  open Microsoft.Extensions.Configuration
  open System

  type Ctx = {
    log  : ILogger
    creds: string
    cId  : Guid
  } 
  with 
    member this.SetCreds creds = { this with creds = creds }

  let initCtx =
    let config = 
      ConfigurationBuilder()
        .AddJsonFile("config.json")
        .Build()
    let log = 
      LoggerConfiguration()
        .ReadFrom
        .Configuration(config)
        .CreateLogger() :> ILogger    
    log.Information "Logger initialized"
    {
      log = log
      creds = ""
      cId = Guid.NewGuid()
    }
