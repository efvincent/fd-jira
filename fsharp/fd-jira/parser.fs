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
  | Help
  | Exit

open Ast

let idOpts = IdentifierOptions()

let ws = spaces
let strWs s = pstringCI s .>> ws
let wsStr s = ws >>. pstringCI s

let issueNum = many1Chars digit   |>> IssueIdent.Num  
let fullIssueId = 
  (many1Chars asciiLetter) .>>. pstring "-" .>>. (many1Chars digit) 
  |>> fun ((p1,p2),p3) -> (p1 + p2 + p3).ToUpper() |> IssueIdent.FullId
let issueId = issueNum <|> fullIssueId  <?> "Issue Identifier"

let countCmd = strWs "count" >>% Command.Count
let helpCmd  = strWs "help"  >>% Command.Help
let exitCmd  = strWs "exit"  >>% Command.Exit

let getCmd = strWs "get" >>. ws >>. issueId |>> Command.Get

let cmdParser = 
  choice 
    [ countCmd
      getCmd
      rangeCmd 
      helpCmd
      exitCmd ] 
  .>> ws 
  .>> eof 
  <?> "Command (use \"help\" for info)"

// This resolves generic value restriction
// see https://www.quanttec.com/fparsec/tutorial.html#fs-value-restriction
// for more information 
run cmdParser "" |> ignore
