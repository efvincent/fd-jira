open System
let tot = 101
let step = 10
let max = if tot % step = 0 then tot else tot + step
let s = seq { for i in step-1 .. 10 .. max do yield i }
s |> Seq.iter (fun n -> printfn "%i" n)

// get first chunk, get the max number from that
// use ^ to do generate a series of parameters
// map those into a sequence of async workflows to do the HTTP call to get chunks
// collect those into single list, map to the keys
// map those into a sequence of async workflows to do the HTTP call to get items and save them to the db

