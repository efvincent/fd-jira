module Parser

open System
open FParsec

module Ast =

  [<RequireQualifiedAccess>]
  type IssueIdent =
  | Num of string
  | FullId of string
  with 
    override this.ToString() = match this with Num s -> s | FullId s -> s

  [<RequireQualifiedAccess>]
  type Command =
  | Count 
  | Get of IssueIdent
  | Range of int * int
  | Sync of DateTime option
  | LastUpd
  | Help
  | Exit

open Ast

let idOpts = IdentifierOptions()

let ws = spaces
let strWs s = ws >>. pstringCI s .>> ws

// Date Parsing. Known sub-optimal, learning FParsec

let pFourDigit:Parser<int,unit> =  
  pipe4 
    digit digit digit digit 
    (fun c1 c2 c3 c4 -> Int32.Parse(sprintf "%c%c%c%c" c1 c2 c3 c4))

let pTwoDigit:Parser<int,unit> = 
  pipe2  
    digit digit (fun c1 c2 -> Int32.Parse(sprintf "%c%c" c1 c2 ))

let pDateDelimiter:Parser<string,unit> = (pstring "/") <|> (pstring "-")

let mkDate (y,m,d) =
  try preturn (DateTime(y,m,d))
  with _ -> fail "Invalid Date"

let pDate1 =
  pipe3 
    pFourDigit (pDateDelimiter >>. pTwoDigit) (pDateDelimiter >>. pTwoDigit)
    (fun y m d -> (y, m, d))
  >>= mkDate

let pDate2 =
  pipe3 
    pTwoDigit (pDateDelimiter >>. pTwoDigit) (pDateDelimiter >>. pFourDigit)
    (fun m d y -> (y, m, d))
  >>= mkDate

let pDate = (attempt pDate2) <|> pDate1

let issueNum = many1Chars digit   |>> IssueIdent.Num  
let fullIssueId = 
  (many1Chars asciiLetter) .>>. pstring "-" .>>. (many1Chars digit) 
  |>> fun ((p1,p2),p3) -> (p1 + p2 + p3).ToUpper() |> IssueIdent.FullId
let issueId = issueNum <|> fullIssueId  <?> "Issue Identifier"

let rangeCmd = strWs "range"       >>% Command.Range (4500, 10)
let countCmd = strWs "count"       >>% Command.Count
let helpCmd  = strWs "help"        >>% Command.Help
let exitCmd  = strWs "exit"        >>% Command.Exit
let lastUpd  = strWs "last update" >>% Command.LastUpd
let getCmd   = strWs "get"         >>. ws >>. issueId |>> Command.Get

let pSyncCmd = strWs "sync" >>% Command.Sync None
let pSyncWithDateCmd = strWs "sync" >>. ws >>. pDate |>> (Some >> Command.Sync)

let syncCmd = (attempt pSyncWithDateCmd) <|> pSyncCmd

let cmdParser = 
  choice 
    [ countCmd
      getCmd
      rangeCmd 
      syncCmd
      lastUpd
      helpCmd
      exitCmd ] 
  .>> ws 
  .>> eof 
  <?> "Command (use \"help\" for info)"

// This resolves generic value restriction
// see https://www.quanttec.com/fparsec/tutorial.html#fs-value-restriction
// for more information 
run cmdParser "" |> ignore
