module Parser

open System
open FParsec

module Ast =

  type Target =
  | Issue
 
  and Keywords =
  | Count of string option
  | Sync of DateTimeOffset option
  | Find of Target * string
  | Target

  