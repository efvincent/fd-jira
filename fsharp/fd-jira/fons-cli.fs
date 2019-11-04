module FonsCli
open System
open Fons.Components

  let getCmdLinePrompt () =
    let curTime = DateTime.Now
    block
      [
        space
        text [fg 0 0 255] "FlightDeck"
        text [fg 220 220 20] "["
        text [fg 250 100 100] (sprintf "%02i:%02i:%02i" curTime.Hour curTime.Minute curTime.Second)
        text [fg 220 220 20] "]"
        space
        text [fg 100 100 100] "$"
        space
      ]