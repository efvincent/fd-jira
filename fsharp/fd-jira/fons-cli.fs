module FonsCli
open System
open Fons.Components

let private style = 
  {|
    sysName     = [fg 100 100 250; bold]
    projectCode = [fg 255 150 120; bold]
    delimiter   = [fg 220 220 20]
    data        = [fg 250 100 100]
    plain       = [fg 100 100 100] 
    keyword     = [fg 150 150 250]
  |}

let getCmdLinePrompt () =
  let curTime = DateTime.Now
  block
    [
      space
      text style.sysName "Jira "
      text style.projectCode "RCTFD "
      text style.delimiter "["
      text style.data (sprintf "%02i:%02i:%02i" curTime.Hour curTime.Minute curTime.Second)
      text style.delimiter "]"
      space
      text style.plain "$"
      space
    ]


let updateSyncStatus completed total =
  block 
    [
      clrLine
      left 100
      text style.keyword "Completed "
      text style.data (string completed)
      text style.keyword " of "
      text style.data (string total)
    ]