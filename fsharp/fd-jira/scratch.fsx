#r @"/Users/eric.vincent/.nuget/packages/fparsec/1.0.4-rc3/lib/netstandard1.6/FParsecCS.dll"
#r @"/Users/eric.vincent/.nuget/packages/fparsec/1.0.4-rc3/lib/netstandard1.6/FParsec.dll"

open FParsec

let test s p =
  match run p s with 
  | Success(v,_,_) -> printfn "Success: %A" v
  | Failure(e,_,_) -> printfn "Failure: %s" e

let pyear:Parser<string,unit> = many1Satisfy isDigit


