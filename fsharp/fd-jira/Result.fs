namespace Microsoft.FSharp.Core
#nowarn "64"

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Result =

    let orFailWith res =
        match res with 
        | Ok v -> v
        | Error err -> failwith err

    let ofObj<'E, 'T when 'T : null> (err: 'E) (value: 'T) =
        match value with null -> Error err | v -> Ok v

    let ofOpt<'T, 'E> (err: 'E) (opt:'T option) =
        match opt with
        | Some v -> Ok v
        | None -> Error err

    let bindAsync (binder:'T -> Async<Result<'U, 'TError>>) (result:Async<Result<'T, 'TError>>) : Async<Result<'U, 'TError>> =
        async {
            let! res = result
            match res with
            | Ok r -> return! binder r
            | Error e -> return Error e
        }

    let bindToAsync (binder:'T -> Async<Result<'U, 'TError>>) (result:Result<'T, 'TError>) : Async<Result<'U, 'TError>> =
        async {
            match result with
            | Ok r -> return! binder r
            | Error e -> return Error e
        }

    let bindFromAsync (binder:'T -> Result<'U, 'TError>) (result:Async<Result<'T, 'TError>>) : Async<Result<'U, 'TError>> =
        async {
            let! result' = result
            return Result.bind binder result'
        }

    let mapAsync (mapper:'T -> Async<'U>) (result:Async<Result<'T, 'TError>>) : Async<Result<'U, 'TError>> =
        async {
            let! result' = result
            match result' with
            | Error e -> return Error e
            | Ok r ->
                let! r' = mapper r
                return Ok r'
        }

    let mapToAsync (mapper:'T -> Async<'U>) (result:Result<'T, 'TError>) : Async<Result<'U, 'TError>> =
        async {
            match result with
            | Error e -> return Error e
            | Ok r ->
                let! r' = mapper r
                return Ok r'
        }

    let mapFromAsync (mapper:'T -> 'U) (result:Async<Result<'T, 'TError>>) : Async<Result<'U, 'TError>> =
        async {
            let! res = result
            match res with
            | Ok r -> return Ok (mapper r)
            | Error e -> return Error e
        }

    let mapErrorAsync (fn:'TError -> Async<'UError>) (result:Async<Result<'T, 'TError>>) : Async<Result<'T, 'UError>> =
        async {
            let! res = result
            match res with
            | Ok r -> return Ok r
            | Error e ->
                let! e' = fn e
                return Error e'
        }

    let mapErrorFromAsync (fn:'TError -> 'UError) (result:Async<Result<'T, 'TError>>) : Async<Result<'T, 'UError>> =
        async {
            let! res = result
            match res with
            | Ok r -> return Ok r
            | Error e ->
                let e' = fn e
                return Error e'
        }

    let iter (iterFn:'T -> unit) (result:Result<'T, 'TError>) : Result<'T, 'TError> =
        match result with
        | Error e -> Error e
        | Ok t ->
            do iterFn t
            Ok t

    let iterAsync (iterFn:'T -> Async<unit>) (result:Async<Result<'T, 'TError>>) : Async<Result<'T, 'TError>> =
        async {
            let! res = result
            match res with
            | Error e -> return Error e
            | Ok t ->
                do! iterFn t
                return Ok t
        }

    let iterError (iterFn:'TError -> unit) (result:Result<'T, 'TError>) : Result<'T, 'TError> =
        match result with
        | Ok t -> Ok t
        | Error e ->
            do iterFn e
            Error e

    let iterErrorAsync (iterFn:'TError -> Async<unit>) (result:Async<Result<'T, 'TError>>) : Async<Result<'T, 'TError>> =
        async {
            let! res = result
            match res with
            | Ok t -> return Ok t
            | Error e ->
                do! iterFn e
                return Error e
        }

    /// Wraps an exception in an Error case
    let wrapExn fnAsync =
        async {
            let! r = fnAsync |> Async.Catch
            match r with
            | Choice1Of2 a -> return Ok a
            | Choice2Of2 ex -> return Error ex
        }

    /// Short circuit fold with list as input
    let foldList fn state source =
        let rec loop acc remainingSource =
            match remainingSource with
            | item::rest ->
                match fn acc item with
                | Ok newAcc -> loop newAcc rest
                | Error e -> Error e
            | [] ->
                Ok acc
        loop state source

    /// Short circuit fold with list as input async
    let foldListAsync fn state source =
        let rec loop acc remainingSource =
            async {
                match remainingSource with
                | item::rest ->
                    let! r = fn acc item
                    match r with
                    | Ok newAcc -> return! loop newAcc rest
                    | Error e -> return Error e
                | [] ->
                    return Ok acc
            }
        async { return! loop state source }

    /// Short circuit mapi with list as input
    let bindiList fn items =
        let result =
            foldList (
                fun iacc item ->
                    let (i,acc) = iacc
                    match fn i item with
                    | Ok v -> Ok (i+1,(v :: acc))
                    | Error e -> Error e
            ) (0,[]) items
        match result with
        | Ok (_, newList) -> Ok (newList |> List.rev)
        | Error e -> Error e

    /// Short circuit mapi with list as input async
    let bindiListAsync fn items =
        async {
            let! result =
                foldListAsync (
                    fun iacc item ->
                        async {
                            let (i,acc) = iacc
                            let! r = fn i item
                            return
                                match r with
                                | Ok v -> Ok (i+1,(v :: acc))
                                | Error e -> Error e
                        }
                ) (0,[]) items
            return
                match result with
                | Ok (_, newList) -> Ok (newList |> List.rev)
                | Error e -> Error e
        }

    /// Short circuit map with list as input
    let bindList fn items = bindiList (fun _ a -> fn a) items

    /// Short circuit map with list as input async
    let bindListAsync fn items = bindiListAsync (fun _ a -> fn a) items

module List =

    let concatResult (results:Result<'T, 'TError> list) : Result<'T list, 'TError> =
        results
        |> List.fold (fun acc res ->
            match acc,res with
            | Error e,_ | _,Error e -> Error e
            | Ok a,Ok r -> Ok (r::a))
            (Ok [])
        |> Result.map List.rev

module Array =

    let concatResult (results:Result<'T, 'TError> []) : Result<'T [], 'TError> =
        results
        |> Array.fold (fun acc res ->
            match acc,res with
            | Error e,_ | _,Error e -> Error e
            | Ok a,Ok r -> Ok (Array.append a [|r|]))
            (Ok [||])

module Seq =
    let concatResult(results:Result<'T, 'Error> seq) : Result<'T seq, 'TError> =
        results
        |> Seq.fold (fun acc res ->
            match acc,res with
            | Error e,_ | _,Error e -> Error e
            | Ok a,Ok r -> Ok (Seq.append a [r]))
            (Ok Seq.empty)