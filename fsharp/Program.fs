open System
open Types.Jira

[<EntryPoint>]
let main argv =
    let creds = 
        match Environment.GetEnvironmentVariable("JIRA_CREDS") |> Option.ofObj with
        | Some s -> 
            let parts = s.Split(':')
            if parts.Length = 2 then 
                Ok (parts.[0], parts.[1]) 
            else 
                Error("Invalid format for JIRA_CREDS")
        | None ->
            Error("Indefined environment variable JIRA_CREDS")

    match creds with
    | Ok(un,pw) -> printfn "creds: %s : %s" un pw
    | Error e -> printfn "Error: %s" e

    let p:Person = { key = "euv0001"; email = "eric@jet.com"; name = "Eric Vincent" } 
    printfn "person is %s" p.name    

    0 