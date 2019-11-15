#r @"/Users/euv0001/.nuget/packages/fparsec/1.0.4-rc3/lib/netstandard1.6/FParsecCS.dll"
#r @"/Users/euv0001/.nuget/packages/fparsec/1.0.4-rc3/lib/netstandard1.6/FParsec.dll"

open FParsec

let test s p =
  match run p s with 
  | Success(v,_,_) -> printfn "Success: %A" v
  | Failure(e,_,_) -> printfn "Failure: %s" e

let pyear:Parser<string,unit> =  pipe4 digit digit digit digit (fun c1 c2 c3 c4 -> sprintf "%c%c%c%c" c1 c2 c3 c4)

test pyear "1234"
