#r @"/Users/euv0001/.nuget/packages/fparsec/1.0.4-rc3/lib/netstandard1.6/FParsecCS.dll"
#r @"/Users/euv0001/.nuget/packages/fparsec/1.0.4-rc3/lib/netstandard1.6/FParsec.dll"

open FParsec
open System

let test p s =
  match run p s with 
  | Success(v,_,_) -> printfn "Success: %A" v
  | Failure(e,_,_) -> printfn "Failure: %s" e

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

test pDate "00-01-0100"

test pDate "0200/20/69"

test pDate "0000/01/01"

test pDate "02/20/1969"

test pDate "2000/10/01"
