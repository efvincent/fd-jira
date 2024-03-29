#nowarn "40"
namespace Marvel

open System
open System.Threading


module Prelude =
  /// Given a value, creates a function with one ignored argument which returns the value.
  let inline konst x _ = x

    /// Given a value, creates a function with two ignored arguments which returns the value.
  let inline konst2 x _ _ = x

module Option =
  /// Folds an option by applying f to Some otherwise returning the default value.
  let inline foldOr (f:'a -> 'b) (defaultValue:'b) = function Some a -> f a | None -> defaultValue

  /// Folds an option by applying f to Some otherwise returning the default value.
  let inline foldOrLazy (f:'a -> 'b) (defaultValue:unit -> 'b) = function Some a -> f a | None -> defaultValue()  
  


// ------------------------------------------------------------------------------------------------------------------
/// Operations on the Result type.
/// NOTE: Choice is a holdover name for Result from the Marvel library (from before there was a Result type in F#)
module Choice =

  /// Maps over the left result type.
  let mapl (f:'a -> 'b) = function
    | Ok a -> f a |> Ok
    | Error e -> Error e

  /// Maps over the right result type.
  let mapr (f:'b -> 'c) = function
    | Ok a -> Ok a
    | Error e -> f e |> Error

  /// Maps over the left or the right result type.
  let bimap (f1:'a -> 'c) (f2:'b -> 'd) = function
    | Ok x -> Ok (f1 x)
    | Error x -> Error (f2 x)

  /// Folds a Result pair with functions for each case.
  let fold (f1:'a -> 'c) (f2:'b -> 'c) = function
    | Ok x -> f1 x
    | Error x -> f2 x

  /// Extracts the value from a result with the same type on the left as the right.
  /// (Also known as the codiagonal morphism).
  let codiag<'a> : Result<'a, 'a> -> 'a =
    fold id id

  /// evaluate f () and return either the result of the evaluation or the exception
  let tryWith f = try Ok (f ()) with exn -> Error exn

  /// Return Some v if is Success v. Otherwise return None.
  let toOption = function
      | Ok a -> Some a
      | Error _ -> None

  /// Return Some v if is Error v. Otherwise return None.
  let toOptionInverse = function
      | Ok _ -> None
      | Error b -> Some b

   /// Return Some v if either is Success v. Otherwise return None.
  let isSuccess = function
      | Ok a -> true
      | Error _ -> false

  let ofOption error = Option.foldOr Ok (Error error)

  let filter (f: 'a -> bool) error = function
      | Ok a ->
          if f a then Ok a
          else Error error
      | Error error -> Error error

  /// Merges two results which can potentially contain errors.
  /// When both results values are errors, they are concatenated using ';'.
  let mergeErrs = function
    | Ok (), Ok () -> Ok ()
    | Error e, Ok _   -> Error e
    | Ok _, Error e   -> Error e
    | Error e1, Error e2 -> Error (String.concat ";" [e1;e2])

  let errorsOfFirst (xs: Result<unit, string> seq) =
    let x0: Result<unit, string> = Ok ()
    xs |> Seq.fold (fun x y ->
      match x, y with
      | Ok _, Ok _ -> Ok ()
      | Ok _, Error y -> Error y
      | Error x, Ok _ -> Error x
      | Error x, Error _ -> Error x
    ) x0

  type ResultBuilder() =
    member __.Return(value) = Ok value
    member __.ReturnFrom(c:Result<'a, 'e>) = c
    member __.Delay(f:unit -> Result<'a, 'e>) = f()
    member __.Bind(c, f) = Result.bind f c
    member this.TryWith(opt, h) =
      try this.ReturnFrom(opt)
      with e -> h e
    member this.TryFinally(opt, compensate) =
      try this.ReturnFrom(opt)
      finally compensate()
    member this.Using(res:#IDisposable, body) =
      this.TryFinally(body res, fun () -> match res with null -> () | disp -> disp.Dispose())

/// A backoff strategy.
/// Accepts the attempt number and returns an interval in milliseconds to wait.
/// If None then backoff should stop.
type Backoff = int -> int option

/// Operations on back off strategies represented as functions (int -> int option)
/// which take an attempt number and produce an interval.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Backoff =
  open Prelude
  let private checkOverflow x =
    if x = System.Int32.MinValue then 2000000000
    else x

  /// Stops immediately.
  let never : Backoff = konst None

  /// Always returns a fixed interval.
  let linear i : Backoff = konst (Some i)

  /// Modifies the interval.
  let bind (f:int -> int option) (b:Backoff) =
    fun i ->
      match b i with
      | Some x -> f x
      | None -> None

  /// Modifies the interval.
  let map (f:int -> int) (b:Backoff) : Backoff =
    fun i ->
      match b i with
      | Some x -> f x |> checkOverflow |> Some
      | None -> None

  /// Bounds the interval.
  let bound mx = map (min mx)

  /// Creates a back-off strategy which increases the interval exponentially.
  let exp (initialIntervalMs:int) (multiplier:float) : Backoff =
    fun i -> (float initialIntervalMs) * (pown multiplier i) |> int |> checkOverflow |> Some

  /// Randomizes the output produced by a back-off strategy:
  /// randomizedInterval = retryInterval * (random in range [1 - randomizationFactor, 1 + randomizationFactor])
  let rand (randomizationFactor:float) =
    let rand = new System.Random()
    let maxRand,minRand = (1.0 + randomizationFactor), (1.0 - randomizationFactor)
    map (fun x -> (float x) * (rand.NextDouble() * (maxRand - minRand) + minRand) |> int)

  /// Uses a fibonacci sequence to genereate timeout intervals starting from the specified initial interval.
  let fib (initialIntervalMs:int) : Backoff =
    let rec fib n =
      if n < 2 then initialIntervalMs
      else fib (n - 1) + fib (n - 2)
    fib >> checkOverflow >> Some

  /// Creates a stateful back-off strategy which keeps track of the number of attempts,
  /// and a reset function which resets attempts to zero.
  let keepCount (b:Backoff) : (unit -> int option) * (unit -> unit) =
    let i = ref -1
    (fun () -> System.Threading.Interlocked.Increment i |> b),
    (fun () -> i := -1)

  /// Bounds a backoff strategy to a specified maximum number of attempts.
  let maxAttempts (max:int) (b:Backoff) : Backoff =
    fun n -> if n > max then None else b n


  // ------------------------------------------------------------------------------------------------------------------------
  // defaults

  /// 500ms
  let [<Literal>] DefaultInitialIntervalMs = 500

  /// 60000ms
  let [<Literal>] DefaultMaxIntervalMs = 60000

  /// 0.5
  let [<Literal>] DefaultRandomizationFactor = 0.5

  /// 1.5
  let [<Literal>] DefaultMultiplier = 1.5

  /// The default exponential and randomized back-off strategy with a provided initial interval.
  /// DefaultMaxIntervalMs = 60,000
  /// DefaultRandomizationFactor = 0.5
  /// DefaultMultiplier = 1.5
  let DefaultExponentialBoundedRandomizedOf initialInternal =
    exp initialInternal DefaultMultiplier
    |> rand DefaultRandomizationFactor
    |> bound DefaultMaxIntervalMs

  /// The default exponential and randomized back-off strategy.
  /// DefaultInitialIntervalMs = 500
  /// DefaultMaxIntervalMs = 60,000
  /// DefaultRandomizationFactor = 0.5
  /// DefaultMultiplier = 1.5
  let DefaultExponentialBoundedRandomized = DefaultExponentialBoundedRandomizedOf DefaultInitialIntervalMs

  // ------------------------------------------------------------------------------------------------------------------------


/// A write-once concurrent variable.
type IVar<'a> = Tasks.TaskCompletionSource<'a>

/// Operations on write-once variables.
module IVar =

  open System.Threading.Tasks

  /// Creates an empty IVar structure.
  let inline create () = new IVar<'a>()

  /// Creates a IVar structure and initializes it with a value.
  let inline createFull a =
    let ivar = create()
    ivar.SetResult(a)
    ivar

  /// Writes a value to an IVar.
  /// A value can only be written once, after which the behavior is undefined and may throw.
  let inline put a (i:IVar<'a>) =
    i.SetResult(a)

  let inline tryPut a (i:IVar<'a>) =
    i.TrySetResult (a)

  /// Writes an error to an IVar to be propagated to readers.
  let inline error (ex:exn) (i:IVar<'a>) =
    i.SetException(ex)

  let inline tryError (ex:exn) (i:IVar<'a>) =
    i.TrySetException(ex)

  /// Writes a cancellation to an IVar to be propagated to readers.
  let inline cancel (i:IVar<'a>) =
    i.SetCanceled()

  let inline tryCancel (i:IVar<'a>) =
    i.TrySetCanceled()

  let private awaitTaskCancellationAsError (t:Task<'a>) : Async<'a> =
    Async.FromContinuations <| fun (ok,err,_) ->
      t.ContinueWith (fun (t:Task<'a>) ->
        if t.IsFaulted then err t.Exception
        elif t.IsCanceled then err (TaskCanceledException("Task wrapped with Async has been cancelled."))
        elif t.IsCompleted then ok t.Result
        else err(Exception "invalid Task state!"))
      |> ignore

  /// Creates an async computation which returns the value contained in an IVar.
  let get (i:IVar<'a>) : Async<'a> =
    i.Task
    |> awaitTaskCancellationAsError

  /// Sets the cancellation token source when the IVar completes.
  let intoCancellationToken (cts:CancellationTokenSource) (i:IVar<_>) =
    i.Task.ContinueWith (fun (t:Tasks.Task<_>) -> cts.Cancel ()) |> ignore

  /// Returns a cancellation token which is cancelled when the IVar is set.
  let toCancellationToken (i:IVar<_>) =
    let cts = new CancellationTokenSource()
    intoCancellationToken cts i
    cts.Token

[<AutoOpen>]
module AsyncExtensions =

    open Prelude
    open System.Threading.Tasks

    type PollState =

      /// The polling condition is met; polling should stop.
      | OK

      /// The polling condition is not met by polling should stop.
      | Yield

      /// Continue polling.
      | Poll

    module AsyncOps =

      let empty : Async<unit> = async.Return()

      let never : Async<unit> = Async.Sleep Timeout.Infinite

      let awaitTaskUnit (t:Task) =
        Async.FromContinuations <| fun (ok,err,cnc) ->
          t.ContinueWith(fun t ->
            if t.IsFaulted then err(t.Exception)
            elif t.IsCanceled then cnc(TaskCanceledException("Task wrapped with Async.AwaitTask has been cancelled.",  t.Exception))
            elif t.IsCompleted then ok()
            else err(Exception "invalid Task state!"))
          |> ignore

      let awaitTaskCancellationAsError (t:Task<'a>) : Async<'a> =
        Async.FromContinuations <| fun (ok,err,_) ->
          t.ContinueWith (fun (t:Task<'a>) ->
            if t.IsFaulted then err t.Exception
            elif t.IsCanceled then err (TaskCanceledException("Task wrapped with Async has been cancelled."))
            elif t.IsCompleted then ok t.Result
            else err(Exception "invalid Task state!"))
          |> ignore

      let awaitTaskUnitCancellationAsError (t:Task) : Async<unit> =
        Async.FromContinuations <| fun (ok,err,_) ->
          t.ContinueWith (fun (t:Task) ->
            if t.IsFaulted then err t.Exception
            elif t.IsCanceled then err (TaskCanceledException("Task wrapped with Async has been cancelled."))
            elif t.IsCompleted then ok ()
            else err(Exception "invalid Task state!"))
          |> ignore

      let awaitTaskCorrect (t:Task<'a>) : Async<'a> =
        Async.FromContinuations <| fun (ok,err,cnc) ->
          t.ContinueWith (fun (t:Task<'a>) ->
            if t.IsFaulted then
                let e = t.Exception
                if e.InnerExceptions.Count = 1 then err e.InnerExceptions.[0]
                else err e
            elif t.IsCanceled then err (TaskCanceledException("Task wrapped with Async has been cancelled."))
            elif t.IsCompleted then ok t.Result
            else err(Exception "invalid Task state!")
          )
          |> ignore

      let awaitTaskUnitCorrect (t:Task) : Async<unit> =
        Async.FromContinuations <| fun (ok,err,cnc) ->
          t.ContinueWith (fun (t:Task) ->
            if t.IsFaulted then
                let e = t.Exception
                if e.InnerExceptions.Count = 1 then err e.InnerExceptions.[0]
                else err e
            elif t.IsCanceled then err (TaskCanceledException("Task wrapped with Async has been cancelled."))
            elif t.IsCompleted then ok ()
            else err(Exception "invalid Task state!")
          )
          |> ignore

    type Async with

        /// An async computation which does nothing and completes immediately.
        static member inline empty = AsyncOps.empty

        /// An async computation which does nothing and never completes.
        static member inline never = AsyncOps.never

        static member map (f:'a -> 'b) (a:Async<'a>) : Async<'b> = async.Bind(a, f >> async.Return)

        static member inline bind (f:'a -> Async<'b>) (a:Async<'a>) : Async<'b> = async.Bind(a, f)

        static member inline join (a:Async<Async<'a>>) : Async<'a> = Async.bind id a

        static member map2 (a:Async<'a>) (b:Async<'b>) (f:'a * 'b -> 'c) = Async.Parallel (a,b) |> Async.map f

        static member inline tryFinally (compensation:unit -> unit) (a:Async<'a>) : Async<'a> =
          async.TryFinally(a, compensation)

        static member inline tryFinallyDispose (d:#IDisposable) (a:Async<'a>) : Async<'a> =
          Async.tryFinally (fun () -> d.Dispose()) a

        static member inline tryFinallyDisposeAll (ds:#IDisposable seq) (a:Async<'a>) : Async<'a> =
          Async.tryFinally (fun () -> ds |> Seq.iter (fun d -> d.Dispose())) a

        static member inline tryCancelled comp a = Async.TryCancelled(a, comp)

        static member inline tryWith h a = async.TryWith(a, h)

        /// Raises supplied exception using Async's exception continuation directly.
        static member Raise<'T> (e : exn) : Async<'T> = Async.FromContinuations(fun (_,ec,_) -> ec e)

        /// Returns an async computation which will wait for the given task to complete.
        static member inline AwaitTask (t:Task) = AsyncOps.awaitTaskUnit t

        /// Returns an async computation which will wait for the given task to complete and returns its result.
        /// Task cancellations are propagated as exceptions so that they can be trapped.
        static member inline AwaitTaskCancellationAsError (t:Task<'a>) : Async<'a> = AsyncOps.awaitTaskCancellationAsError t

        /// Returns an async computation which will wait for the given task to complete and returns its result.
        /// Task cancellations are propagated as exceptions so that they can be trapped.
        static member inline AwaitTaskCancellationAsError (t:Task) : Async<unit> = AsyncOps.awaitTaskUnitCancellationAsError t

        /// Asynchronously await supplied task with the following variations:
        ///     *) Task cancellations are propagated as exceptions
        ///     *) Singleton AggregateExceptions are unwrapped and the offending exception passed to cancellation continuation
        static member inline AwaitTaskCorrect (task:Task) : Async<unit> = AsyncOps.awaitTaskUnitCorrect task

        /// Asynchronously await supplied task with the following variations:
        ///     *) Task cancellations are propagated as exceptions
        ///     *) Singleton AggregateExceptions are unwrapped and the offending exception passed to cancellation continuation
        static member inline AwaitTaskCorrect (task:Task<'T>) : Async<'T> = AsyncOps.awaitTaskCorrect task

        /// Like Async.StartWithContinuations but starts the computation on a ThreadPool thread.
        static member StartThreadPoolWithContinuations (a:Async<'a>, ok:'a -> unit, err:exn -> unit, cnc:OperationCanceledException -> unit, ?ct:CancellationToken) =
          let a = Async.SwitchToThreadPool () |> Async.bind (fun _ -> a)
          Async.StartWithContinuations (a, ok, err, cnc, defaultArg ct CancellationToken.None)

        static member Parallel (c1, c2) : Async<'a * 'b> = async {
            let! c1 = c1 |> Async.StartChild
            let! c2 = c2 |> Async.StartChild
            let! c1 = c1
            let! c2 = c2
            return c1,c2 }

        static member Parallel (c1:Async<unit>, c2:Async<unit>) : Async<unit> = async {
            let! c1 = c1 |> Async.StartChild
            let! c2 = c2 |> Async.StartChild
            do! c1
            do! c2 }

        static member Parallel (c1, c2, c3) : Async<'a * 'b * 'c> = async {
            let! c1 = c1 |> Async.StartChild
            let! c2 = c2 |> Async.StartChild
            let! c3 = c3 |> Async.StartChild
            let! c1 = c1
            let! c2 = c2
            let! c3 = c3
            return c1,c2,c3 }

        static member Parallel (c1, c2, c3, c4) : Async<'a * 'b * 'c * 'd> = async {
            let! c1 = c1 |> Async.StartChild
            let! c2 = c2 |> Async.StartChild
            let! c3 = c3 |> Async.StartChild
            let! c4 = c4 |> Async.StartChild
            let! c1 = c1
            let! c2 = c2
            let! c3 = c3
            let! c4 = c4
            return c1,c2,c3,c4 }

        /// <summary>
        /// Creates a computation which executes the specified computations sinks in parallel with the specified degree of parallelism.
        /// This is a memory conserving alternative to Async.Parallel for when the computations are sinks such that the results can be discarded.
        /// </summary>
        /// <remarks>
        /// There are several notable guarantees provided by this scheduler. The sequence of input computations will be
        /// iterated in sequential order and each computation will be started on the calling thread. The computation will
        /// execute on the calling thread until it reaches an async boundary at which point the Async trampoline takes over.
        /// These guarantees are important for consumers which require ordering guarantees.
        /// Note, this will be inefficient for CPU bound tasks, since the initial part of each computation will be run on a single thread and subsequent computations will have to wait.
        /// </remarks>
        static member withParallelWorkers (parallelism:int) (ct:CancellationToken) (comps:seq<Async<unit>>) = async {

            let sm = new SemaphoreSlim(parallelism)
            let cde = new CountdownEvent(1)
            //let tcs = new Tasks.TaskCompletionSource<unit>()

            let inline release() =
                sm.Release() |> ignore
                cde.Signal() |> ignore

            let inline cont() =
                release()

            let inline exCont (ex:exn) =
                //tcs.SetException(ex)
                ////Log.error "Error within Async.withParallelWorkers: %O" ex
                release()

            let inline cnCont (ex:OperationCanceledException) =
                //tcs.SetException(ex)
                //Log.error "Error within Async.withParallelWorkers: %O" ex
                release()

            try

                for computation in comps do
                    if not ct.IsCancellationRequested then
                        sm.Wait(ct)
                        cde.AddCount(1)
                        Async.StartWithContinuations(computation, cont, exCont, cnCont, ct)

                cde.Signal() |> ignore // dummy call
                cde.Wait()

            finally

                cde.Dispose()
                sm.Dispose()

        }

        /// Creates a computation which executes the specified computations sinks in parallel with unbounded parallelism.
        /// Note: almost always you want to run the throttled variant because if the computations are being produced faster
        /// than they complete, OOM is imminent.
        static member withParallelWorkersUnbounded (comps:seq<Async<unit>>) = async {

            let cde = new CountdownEvent(1)

            let inline release() =
                cde.Signal() |> ignore

            let inline cont() =
                release()

            let inline exCont (ex:exn) =
                //Log.error "Error within Async.withParallelWorkersUnbounded: %O" ex
                release()

            let inline cnCont ex =
                //Log.error "Error within Async.withParallelWorkersUnbounded: %O" ex
                release()

            try

                for computation in comps do
                    cde.AddCount(1)
                    Async.StartWithContinuations(computation, cont, exCont, cnCont)

                cde.Signal() |> ignore
                cde.Wait()

            finally

                cde.Dispose()

        }

        static member ParallelThrottledIgnore (startOnCallingThread:bool) (parallelism:int) (xs:seq<Async<_>>) = async {
          let! ct = Async.CancellationToken
          let sm = new SemaphoreSlim(parallelism)
          let count = ref 1
          let res = IVar.create ()
          let tryWait () =
            try sm.Wait () ; true
            with _ -> false

          let tryComplete () =
            if Interlocked.Decrement count = 0 then
              IVar.tryPut () res |> ignore
              false
            else
              not res.Task.IsCompleted

          let ok _ =
            if tryComplete () then
              // sm can be disposed when an error/cancellation completes IVar
              // after the res.Task.IsCompleted is read by tryComplete ()
              try sm.Release () |> ignore with _ -> ()
          let err (ex:exn) = IVar.tryError ex res |> ignore
          let cnc (_:OperationCanceledException) = IVar.tryCancel res |> ignore

          let start = async {
            use en = xs.GetEnumerator()
            while not (res.Task.IsCompleted) && en.MoveNext() do
              if tryWait () then
                Interlocked.Increment count |> ignore
                if startOnCallingThread then Async.StartWithContinuations (en.Current, ok, err, cnc, ct)
                else Async.StartThreadPoolWithContinuations (en.Current, ok, err, cnc, ct)
            tryComplete () |> ignore }
          Async.Start (Async.tryWith (err >> async.Return) start, ct)
          return! res.Task |> Async.AwaitTaskCancellationAsError }

        /// Creates an async computation which runs the provided sequence of computations and completes
        /// when all computations in the sequence complete. Up to parallelism computations will
        /// be in-flight at any given point in time. Error or cancellation of any computation in
        /// the sequence causes the resulting computation to error or cancel, respectively.
        static member ParallelIgnoreCT (ct:CancellationToken) (parallelism:int) (xs:seq<Async<_>>) = async {
          let sm = new SemaphoreSlim(parallelism)
          let cde = new CountdownEvent(1)
          let tcs = new TaskCompletionSource<unit>()
          ct.Register(Action(fun () -> tcs.TrySetCanceled() |> ignore)) |> ignore

          let tryComplete () =
            if cde.Signal() then
              tcs.SetResult(())

          let inline ok _ =
            if not (tcs.Task.IsCompleted) then
              sm.Release() |> ignore
              tryComplete ()

          let inline err (ex:exn) =
            sm.Release() |> ignore
            tcs.TrySetException ex |> ignore

          let inline cnc (_:OperationCanceledException) =
            sm.Release() |> ignore
            tcs.TrySetCanceled () |> ignore
          try
            use en = xs.GetEnumerator()
            while not (tcs.Task.IsCompleted) && en.MoveNext() do
              sm.Wait()
              cde.AddCount(1)
              Async.StartWithContinuations (en.Current, ok, err, cnc, ct)
            tryComplete ()
            do! tcs.Task |> Async.AwaitTaskCancellationAsError
          finally
            cde.Dispose()
            sm.Dispose() }

        /// Creates an async computation which runs the provided sequence of computations and completes
        /// when all computations in the sequence complete. Up to parallelism computations will
        /// be in-flight at any given point in time. Error or cancellation of any computation in
        /// the sequence causes the resulting computation to error or cancel, respectively.
        static member ParallelIgnore (parallelism:int) (xs:seq<Async<_>>) =
          Async.ParallelIgnoreCT CancellationToken.None parallelism xs

        /// Creates an async computation which runs the provided sequence of computations and completes
        /// when all computations in the sequence complete. Up to parallelism computations will
        /// be in-flight at any given point in time. Error or cancellation of any computation in
        /// the sequence causes the resulting computation to error or cancel, respectively.
        /// Like Async.Parallel but with support for throttling.
        /// Note that an array is allocated to contain the results of all computations.
        static member ParallelThrottled (parallelism:int) (tasks:seq<Async<'T>>) : Async<'T[]> = async {
            if parallelism < 1 then invalidArg "parallelism" "Must be positive number."
            use semaphore = new SemaphoreSlim(parallelism)
            let throttledWorker (task:Async<'T>) = async {
                let! ct = Async.CancellationToken
                do! semaphore.WaitAsync ct |> Async.AwaitTaskCorrect
                try return! task
                finally ignore(semaphore.Release())
            }

            return! tasks |> Seq.map throttledWorker |> Async.Parallel
        }

        /// Creates an async computation which runs the sequence of provided computations returning
        /// results immediately as they arrive. Up to parallelism of computations will be in-flight
        /// at any point in time. If the resulting sequence isn't consumed, additional computations from
        /// the source sequence won't be started.
        static member ParallelThrottledYield (parallelism:int) (s:seq<Async<'a>>) : Async<seq<'a>> = async {
          let buffer = new Collections.Concurrent.BlockingCollection<_>(parallelism)
          let cts = new CancellationTokenSource()
          try
            do! Async.ParallelIgnoreCT cts.Token parallelism (s |> Seq.map (Async.map buffer.Add))
          finally
            buffer.CompleteAdding()
          return seq {
            use buffer = buffer
            use cts = cts
            use _cancel = { new IDisposable with member __.Dispose() = cts.Cancel() }
            yield! buffer.GetConsumingEnumerable(cts.Token) } }

        /// Given a function returning an async computation, return a unit returning function (sink) which
        /// executes the async function internally through a blocking buffer.
        static member toSyncStop (maxQueueSize:int) (workers:int) (f:'a -> Async<unit>) =
            let buffer = new System.Collections.Concurrent.BlockingCollection<_>(maxQueueSize)
            buffer.GetConsumingEnumerable()
            |> Seq.map f
            |> Async.withParallelWorkers workers CancellationToken.None
            |> Async.Start
            buffer.Add , buffer.CompleteAdding

        /// Given a function returning an async computation, return a unit returning function (sink) which
        /// executes the async function internally through a blocking buffer.
        static member toSync (maxBufferSize:int) (workers:int) (f:'a -> Async<unit>) =
            Async.toSyncStop maxBufferSize workers f |> fst

        /// Retries an async computation. The filter predicate should return true if this should retry and false if this should not retry.
        static member retryBackoff (attempts:int) (filter:exn -> bool) (backoff:int -> int option) (a:Async<'a>) =
          let rec go i (ts: int list) = async {
            try
              let! res = a
              return res
            with ex when filter ex ->
              if (i = attempts) then return raise (new Exception(sprintf "Retry failed after %i attempts. %s" i (String.Join(" ", ts)), ex))
              else
                match backoff i with
                | Some timeoutMs when timeoutMs > 0 ->
                  do! Async.Sleep timeoutMs
                  return! go (i + 1) (timeoutMs :: ts)
                | _ ->
                  return! go (i + 1) ts
            }
          go 1 []

        /// Retries an async computation.
        static member retryAllBackoff (attempts:int) (backoff:int -> int option) (a:Async<'a>) =
          Async.retryBackoff attempts (Prelude.konst true) backoff a

        /// Retries an async computation.
        static member retryTimeout (attempts:int) (filter:exn -> bool) (timeoutMs:int) (a:Async<'a>) = async {
            try
                let! res = a
                return res
            with ex ->
                if (filter ex = false) then return raise (new Exception("Retry attempt exception filtered.", ex))
                elif attempts = 0 then return raise (new Exception("Retry failed after several attempts.", ex))
                else
                    if timeoutMs > 0 then do! Async.Sleep timeoutMs
                    return! Async.retryTimeout (attempts - 1) filter timeoutMs a
        }

        /// Retries an async computation when exceptions match the specified filter.
        static member retry (attempts:int) (filter:exn -> bool) (a:Async<'a>) = Async.retryTimeout attempts filter 0 a

        /// Retries an async computation given any exception.
        static member retryAll (attempts:int) (a:Async<'a>) = Async.retry attempts (fun _ -> true) a

        /// Retries an async computation when exceptions match the specified filter, performs an action after a number of retries, then continues until fixed.
        static member retryIndefinitelyWithFault attemptsBeforeDeclaringFault retryFilter backoffStrategyBeforeFault retryDelayWhenFaultedMs declareFault declareFaultFixed (a:Async<'a>) = async {
                try
                    return! a |> Async.retryBackoff attemptsBeforeDeclaringFault retryFilter backoffStrategyBeforeFault
                with ex ->
                    let origEx = ex.InnerException
                    if origEx = Unchecked.defaultof<_> then return raise (new Exception("Retry attempt exception filtered", ex))
                    elif (retryFilter origEx = false) then return raise (new Exception("Retry attempt exception filtered", origEx))
                    else
                        let! declaredFaultContext = declareFault attemptsBeforeDeclaringFault retryDelayWhenFaultedMs origEx
                        let rec retryIndefinitely retryFilter delayMs (a:Async<'a>)  = async {
                                try
                                    do! Async.Sleep delayMs
                                    return! a
                                with newEx ->
                                    if (retryFilter newEx = false) then return raise (new Exception("Retry attempt exception filtered", newEx))
                                    return! retryIndefinitely retryFilter delayMs a
                            }
                        let result = a |> retryIndefinitely retryFilter retryDelayWhenFaultedMs |> Async.RunSynchronously
                        do! declareFaultFixed declaredFaultContext
                        return result

            }

        /// Retries an async computation given any exception, performs an action after a number of retries, then continues until fixed.
        static member retryAllIndefinitelyWithFault attemptsBeforeDeclaringFault backoffStrategyBeforeFault retryDelayWhenFaultedMs declareFault declareFaultFixed a = async {
                return! Async.retryIndefinitelyWithFault attemptsBeforeDeclaringFault (fun _ -> true) backoffStrategyBeforeFault retryDelayWhenFaultedMs declareFault declareFaultFixed a
            }

        /// Starts the specified operation using a new CancellationToken and returns
        /// IDisposable object that cancels the computation. This method can be used
        /// when implementing the Subscribe method of IObservable interface.
        static member StartDisposable (op:Async<unit>) =
            let ct = new System.Threading.CancellationTokenSource()
            Async.Start(op, ct.Token)
            { new IDisposable with member x.Dispose() = ct.Cancel() }

        /// Modifies an async computation such that it can be cancelled by the specified cancellation token.
        static member withCancellationToken (ct:CancellationToken) (a:Async<'a>) : Async<'a> = async {
          let tcs = new TaskCompletionSource<_>()
          Async.StartWithContinuations (a, tcs.SetResult, tcs.SetException, (fun _ -> tcs.SetCanceled()), ct)
          return! tcs.Task |> Async.AwaitTask }

        /// Returns an async computation which runs the argument computation but raises an exception if it doesn't complete
        /// by the specified timeout.
        static member timeoutAfter (timeout:TimeSpan) (c:Async<'a>) = async {
            let! r = Async.StartChild(c, (int)timeout.TotalMilliseconds)
            return! r }

        /// Returns an async computation which runs the argument computation but returns a Error if it doesn't complete
        /// by the specified timeout.
        static member timeoutAfterEx (timeout:TimeSpan) (c:Async<'a>) =
            Async.timeoutAfter timeout c |> Async.Catch

        /// Async/Option transformer map.
        static member mapOpt (f:'a -> 'b option) (a:Async<'a option>) : Async<'b option> =
            a |> Async.map (Option.foldOr f None)

        /// Async/Option transformer bind.
        static member bindOpt (f:'a -> Async<'b option>) (a:Async<'a option>) : Async<'b option> =
            a |> Async.bind (Option.foldOr f (async.Return None))

        static member mapResult (f: 'a -> Result<'b,'c>) (a:Async<Result<'a,'c>>) =
            a |> Async.map (function
                | Ok a' -> f a'
                | Error error -> Error error)

        static member bindResult (f: 'a -> Async<Result<'b,'c>>) (a:Async<Result<'a,'c>>)  =
            a |> Async.bind (function
                | Ok a' -> f a'
                | Error error -> async.Return (Error error))

        static member sleepAfter ms (a:Async<_>) : Async<_> = a |> Async.bind (fun a -> Async.Sleep ms |> Async.map (konst a))

        static member sleepBefore ms (a:Async<_>) : Async<_> = Async.Sleep ms |> Async.bind (konst a)

        /// Ensures that the function is invoked serially - no overlapping calls.
        static member serialize (f:'a -> Async<'b>) =

          // TODO: dispose agent?
          let agent = MailboxProcessor.Start <| fun agent ->
            let rec loop() = async {
              let! (a,ch:AsyncReplyChannel<'b>) = agent.Receive()
              let! b = f a
              ch.Reply b
              return! loop()
            }
            loop()

          fun a -> agent.PostAndAsyncReply(fun ch -> a,ch)

        /// <summary>
        /// Takes a value and an async computation and creates an async computation which includes the original result and the value.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <remarks>http://en.wikipedia.org/wiki/Strong_monad</remarks>
        static member strength (c:Async<'b>) (a:'a) : Async<'a * 'b> =
          c |> Async.map (fun b -> a,b)

        /// The opposite of Async.Catch - will raise an erroneous result as an exception.
        static member throw (a:Async<Result<'a, exn>>) : Async<'a> =
          a |> Async.map (function Ok a -> a | Error e -> raise e)

        /// Caches a computation such that it is only invoked once.
        static member cache (a:Async<'a>) : Async<'a> =
          let tcs = TaskCompletionSource<'a>()
          let state = ref 0
          async {
            if (Interlocked.CompareExchange(state, 1, 0) = 0) then
              Async.StartWithContinuations(a, tcs.SetResult, tcs.SetException, (fun _ -> tcs.SetCanceled()))
            return! tcs.Task |> Async.AwaitTask
          }

        /// Cache a function's async result for each argument to reduce expensive and repetitive
        /// computation of an asynchronous operation. Uses a concurrent dictionary for backing
        /// storage, and at-least-once invocation semantics per key.
        static member memoize (f:'a->Async<'b>) : 'a->Async<'b> =
            let dict = System.Collections.Concurrent.ConcurrentDictionary()
            fun x -> async {
                match dict.TryGetValue x with
                | true, result -> return result
                | false, _ ->
                    let! result = f x
                    dict.TryAdd(x, result) |> ignore
                    return result
            }

        /// Creates a computation which returns the result of the first computation that
        /// produces a value as well as a handle to the other computation. The other
        /// computation will be memoized.
        static member chooseBoth (a:Async<'a>) (b:Async<'a>) : Async<'a * Async<'a>> =
          Async.FromContinuations <| fun (ok,err,cnc) ->
            let state = ref 0
            let iv = new TaskCompletionSource<_>()
            let inline ok a =
              if (Interlocked.CompareExchange(state, 1, 0) = 0) then
                ok (a, iv.Task |> Async.AwaitTask)
              else
                iv.SetResult a
            let inline err (ex:exn) =
              if (Interlocked.CompareExchange(state, 1, 0) = 0) then err ex
              else iv.SetException ex
            let inline cnc ex =
              if (Interlocked.CompareExchange(state, 1, 0) = 0) then cnc ex
              else iv.SetCanceled ()
            Async.StartThreadPoolWithContinuations (a, ok, err, cnc)
            Async.StartThreadPoolWithContinuations (b, ok, err, cnc)

        /// Creates a computation which returns the result of the first computation that
        /// produces a value or the failures if neither returns a value.
        static member chooseBothFromResult (a:Async<Result<'a, 'b>>) (b:Async<Result<'a, 'b>>) : Async<Result<'a, 'b * 'b>> =
          async {
            let! a, bh = Async.chooseBoth a b
            match a with
            | Ok a_s ->
              return Ok a_s
            | Error a_f ->
              let! b = bh
              return Choice.mapr (fun b_f -> (a_f, b_f)) b
          }

        static member chooseBothFromResult1 (a:'c -> Async<Result<'a, 'b>>) (b:'c -> Async<Result<'a, 'b>>) : 'c -> Async<Result<'a, 'b * 'b>> =
          fun c -> Async.chooseBothFromResult (a c) (b c)

        static member chooseBothFromResult2 (a:'d -> 'c -> Async<Result<'a, 'b>>) (b:'d -> 'c -> Async<Result<'a, 'b>>) : 'd -> 'c -> Async<Result<'a, 'b * 'b>> =
          fun d c -> Async.chooseBothFromResult (a d c) (b d c)

        static member chooseTasks (a:Task<'a>) (b:Task<'a>) : Async<'a * Task<'a>> = async {
          let! ct = Async.CancellationToken
          let i = Task.WaitAny([| (a :> Task) ; (b :> Task) |], ct)
          if i = 0 then return (a.Result, b)
          elif i = 1 then return (b.Result, a)
          else return! failwith (sprintf "unreachable, i = %d" i) }

        /// Creates a computation which produces a tuple consiting of the value produces by the first
        /// argument computation to complete and a handle to the other computation. The second computation
        /// to complete is memoized.
        static member internal chooseBothAny (a:Async<'a>) (b:Async<'b>) : Async<Result<'a * Async<'b>, 'b * Async<'a>>> =
          Async.chooseBoth (a |> Async.map Ok) (b |> Async.map Error)
          |> Async.map (fun (first,second) ->
            match first with
            | Ok a -> (a,(second |> Async.map (function Error b -> b | _ -> failwith "invalid state"))) |> Ok
            | Error b -> (b,(second |> Async.map (function Ok a -> a | _ -> failwith "invalid state"))) |> Error
          )

        static member chooseWithToken (a:Async<'a>) (b:Async<'a>) (token: CancellationToken) : Async<'a> =
           Async.FromContinuations <| fun (ok,err,cnc) ->
            let state = ref 0
            let cts = CancellationTokenSource.CreateLinkedTokenSource(token)
            let inline cancel () =
              cts.Cancel()
              cts.Dispose()
            let inline ok a =
              if (Interlocked.CompareExchange(state, 1, 0) = 0) then
                cancel ()
                ok a
            let inline err (ex:exn) =
              if (Interlocked.CompareExchange(state, 1, 0) = 0) then
                cancel ()
                err ex
            let inline cnc ex =
              if (Interlocked.CompareExchange(state, 1, 0) = 0) then
                cancel ()
                cnc ex
            Async.StartThreadPoolWithContinuations (a, ok, err, cnc, cts.Token)
            Async.StartThreadPoolWithContinuations (b, ok, err, cnc, cts.Token)

        /// Creates an async computation which completes when any of the argument computations completes.
        /// The other argument computation is cancelled.
        static member choose (a:Async<'a>) (b:Async<'a>) : Async<'a> =
          let cts = new CancellationTokenSource()
          Async.chooseWithToken a b cts.Token

        static member chooseAny (xs:Async<'a> seq) : Async<'a> =
          xs |> Seq.reduce Async.choose

        static member liftFst (a:'a, b:Async<'b>) : Async<'a * 'b> =
          b |> Async.map (fun b -> a,b)

        static member liftSnd (a:Async<'a>, b:'b) : Async<'a * 'b> =
          a |> Async.map (fun a -> a,b)

        /// Converts an async computation returning a Result where Ok represents Success
        /// and Error represents Error such that failures are raised as exceptions.
        static member throwMap (f:'e -> exn) (a:Async<Result<'a, 'e>>) : Async<'a> = async {
          let! r = a
          match r with
          | Ok a -> return a
          | Error ex -> return raise (f ex) }

        /// Creates an async computation which runs the provided computation until a condition is met.
        /// Returns Ok if the condition is met and Error if the condition function halts.
        static member pollAsync (condition:'a -> Async<PollState>) (a:Async<'a>) : Async<Result<'a, 'a>> =
          let rec go () = async {
            let! r = a
            let! b = condition r
            match b with
            | OK -> return Ok r
            | Yield -> return Error r
            | Poll -> return! go () }
          go ()

        /// Creates an async computation which runs the provided computation until a condition is met.
        /// The backoff strategy determines sleep time between poll attempts.
        /// If the backoff strategy returns None then polling stops and Error is returned.
        static member pollBackoff (condition:'a -> bool) (backoff:Backoff) (a:Async<'a>) : Async<Result<'a, 'a>> =
          let getBackoff,resetBackoff = Backoff.keepCount backoff
          a |> Async.pollAsync (fun a -> async {
            let c = condition a
            if c then
              resetBackoff ()
              return PollState.OK
            else
              match getBackoff () with
              | Some backoffMs ->
                if backoffMs > 0 then
                  do! Async.Sleep backoffMs
                return PollState.Poll
              | None ->
                return PollState.Yield
          })

        /// Creates an async computation which runs the specified computation until it returns Success (Ok)
        /// or until the backoff strategy is depleted.
        static member pollSuccessBackoff (backoff:Backoff) (a:Async<Result<'a, 'e>>) : Async<Result<'a, 'e>> =
          a |> Async.pollBackoff (Choice.fold (konst true) (konst false)) backoff |> Async.map Choice.codiag

        /// Creates an async computation which runs the specified computation until it returns Success (Ok)
        /// or until the maximum number of attempts have been made.
        static member pollSuccessMax (maxAttempts:int) (a:Async<Result<'a, 'e>>) : Async<Result<'a, 'e>> =
          Async.pollSuccessBackoff (Backoff.linear 0 |> Backoff.maxAttempts maxAttempts) a

        /// Creates an async computation which runs the argument computation until it returns Some.
        static member pollPick (a:Async<'a option>) : Async<'a> =
          async {
            let! x = a
            match x with
            | Some a -> return a
            | None -> return! Async.pollPick a }

        /// Runs the async computation and blocks the calling thread until it completes using TaskCompletionSource<'a>.
        /// This is an alternative to Async.RunSynchronously which was demonstarted to perform significantly
        /// better in certain scenarions (https://github.com/Microsoft/visualfsharp/issues/581).
        static member run (a:Async<'a>) =
          let tcs = new TaskCompletionSource<'a>()
          Async.StartWithContinuations(a, tcs.SetResult, tcs.SetException, fun _ -> tcs.SetCanceled ())
          tcs.Task.Result

        /// Returns an async computation which runs the argument computation and if it completes before the timeout
        /// returns Some. Returns None if the computation times out.
        /// Reference: Async.choose (Async.map Some a) (Async.map (konst None) (Async.Sleep timeoutMs))
        static member timeoutNone (timeoutMs:int) (a:Async<'a>) : Async<'a option> = async {
          let! ct = Async.CancellationToken
          let res = IVar.create ()
          use cts = CancellationTokenSource.CreateLinkedTokenSource ct
          IVar.intoCancellationToken cts res
          use timer = new Timer((fun _ -> IVar.tryPut None res |> ignore), null, timeoutMs, Timeout.Infinite)
          Async.StartThreadPoolWithContinuations (
            a,
            (fun a -> IVar.tryPut (Some a) res |> ignore),
            (fun e -> IVar.tryError e res |> ignore),
            (fun _ -> IVar.tryPut None res |> ignore),
            cts.Token)
          return! res |> IVar.get }

/// A sequence of async computations.
type AsyncParSeq<'a> = Async<seq<Async<'a>>>

module AsyncParSeq =

    let unit<'a> : AsyncParSeq<'a> = async.Return Seq.empty

    let singleton a : AsyncParSeq<'a> = a |> Seq.singleton |> async.Return

    let map (f:'a -> 'b) (s:AsyncParSeq<'a>) : AsyncParSeq<'b> =
        s |> Async.map (Seq.map (Async.map f))

    let append (s1:AsyncParSeq<'a>) (s2:AsyncParSeq<'a>) : AsyncParSeq<'a> = async {
        let! s1 = s1
        let! s2 = s2
        return Seq.append s1 s2 }

    let concat (s:AsyncParSeq<AsyncParSeq<'a>>) : AsyncParSeq<'a> = async {
        let! s = s |> Async.bind (Seq.map Async.join >> Async.Parallel)
        return s |> Seq.concat }

//    let concatSeq (s:AsyncParSeq<seq<'a>>) : AsyncParSeq<'a> = async {
//        let! s = s
//        let s = s |> Seq.map (Async.m)
//
//        return failwith "" }

    let collect (f:'a -> AsyncParSeq<'b>) (s:AsyncParSeq<'a>) : AsyncParSeq<'b> =
        s |> map f |> concat

    let withParallelWorkers (parallelism:int) (ct:CancellationToken) (f:'a -> Async<unit>) (comp:AsyncParSeq<'a>) =
        comp |> Async.bind (Seq.map (Async.bind f) >> Async.withParallelWorkers parallelism ct)

    let iterParThrottled (parallelism:int) (f:'a -> Async<unit>) (comp:AsyncParSeq<'a>) =
        comp |> Async.bind (Seq.map (Async.bind f) >> Async.withParallelWorkers parallelism CancellationToken.None)

[<AutoOpen>]
module AsyncBuilders =

  type AsyncOptionBuilder () =
    member x.Zero () : Async<option<'a>> = async.Return (None)
    member x.Return (c:option<'a>) : Async<option<'a>> = async.Return c
    member x.ReturnFrom (c:Async<option<'a>>) : Async<option<'a>> = c
    member x.Delay (f:unit -> Async<option<'a>>) : Async<option<'a>> = async.Delay f
    member x.Bind (computation:Async<option<'a>>, binder:'a -> Async<option<'b>>) : Async<option<'b>> =
      async {
        let! c = computation
        match c with
        | Some a -> return! binder a
        | None -> return None }
    member x.Bind (computation:Async<'a>, binder:'a -> Async<option<'b>>) : Async<option<'b>> =
      async.Bind (computation, binder)
    member x.Bind (c:option<'a>, binder:'a -> Async<option<'b>>) : Async<option<'b>> =
      async {
        match c with
        | Some a -> return! binder a
        | None -> return None }
    member x.Bind (c:option<Async<'a>>, binder:'a -> Async<option<'b>>) : Async<option<'b>> =
      async {
        match c with
        | Some a ->
          let! a = a
          return! binder a
        | None -> return None }
    member x.TryWith (computation:Async<option<'a>>, catchHandler:exn -> Async<option<'a>>) : Async<option<'a>> =
      async.TryWith (computation, catchHandler)
    member x.TryFinally (computation:Async<option<'a>>, compensation:unit -> unit) : Async<option<'a>> =
      async.TryFinally (computation, compensation)
    member x.Combine (computation1:Async<option<unit>>, computation2:Async<option<'a>>) : Async<option<'a>> =
      async {
        let! c = computation1
        match c with
        | Some _ -> return! computation2
        | None -> return None }
    
    member x.Using (resource:'a, binder:'a -> Async<'a>) : Async<option<'a>> =
      async.Using (resource, binder >> Async.map Some)

    member x.Using (resource:'a, binder:'a -> Async<option<'a>>) : Async<option<'a>> =
      async.Using (resource, binder)
    
    member x.For (sequence:seq<'a>, body:'a -> Async<unit>) : Async<unit> =
      async.For(sequence, body)

    member x.While (guard:unit -> bool, computation:Async<unit>) : Async<unit> =
      async.While (guard, computation)

    member x.While (guard:unit -> bool, computation:Async<Result<unit, 'e list>>) : Async<Result<unit, 'e list>> =
      async {
        let errs = ResizeArray<_>()
        while guard () do
          let! r = computation
          match r with
          | Ok () -> ()
          | Error e -> errs.AddRange e
        if (errs.Count > 0) then return Error (errs |> List.ofSeq)
        else return Ok () }

  /// Async workflow builder for Async<Option<'a>>
  let asyncOption = new AsyncOptionBuilder ()

  type AsyncResultBuilder () =
    member x.Zero () : Async<Result<unit, 'e list>> = async.Return (Ok ())
    member x.Return (c:Result<'a, 'e list>) : Async<Result<'a, 'e list>> = async.Return c
    member x.ReturnFrom (c:Async<Result<'a, 'e list>>) : Async<Result<'a, 'e list>> = c
    member x.Delay (f:unit -> Async<Result<'a, 'e list>>) : Async<Result<'a, 'e list>> = async.Delay f
    member x.Bind (computation:Async<'a>, binder:'a -> Async<Result<'b, 'e list>>) : Async<Result<'b, 'e list>> =
      async.Bind (computation, binder)
    member x.Bind (computation:Async<Result<'a, 'e list>>, binder:'a -> Async<Result<'b, 'e list>>) : Async<Result<'b, 'e list>> =
      async {
        let! c = computation
        match c with
        | Ok a -> return! binder a
        | Error e -> return Error e }
    member x.Bind (c:Result<'a, 'e list>, binder:'a -> Async<Result<'b, 'e list>>) : Async<Result<'b, 'e list>> =
      async {
        match c with
        | Ok a -> return! binder a
        | Error e -> return Error e }
    member x.TryWith (computation:Async<Result<'a, 'e list>>, catchHandler:exn -> Async<Result<'a, 'e list>>) : Async<Result<'a, 'e list>> =
      async.TryWith (computation, catchHandler)
    member x.TryFinally (computation:Async<Result<'a, 'e list>>, compensation:unit -> unit) : Async<Result<'a, 'e list>> =
      async.TryFinally (computation, compensation)
    member x.Combine (computation1:Async<Result<unit, 'e list>>, computation2:Async<Result<'a, 'e list>>) : Async<Result<'a, 'e list>> =
      async {
        let! c = computation1
        match c with
        | Ok _ -> return! computation2
        | Error e -> return Error e }
    member x.Using (resource:'a, binder:'a -> Async<'a>) : Async<Result<'a, 'e list>> =
      async.Using (resource, binder >> Async.map Ok)
    member x.Using (resource:'a, binder:'a -> Async<Result<'a, 'e list>>) : Async<Result<'a, 'e list>> =
      async.Using (resource, binder)
    member x.For (sequence:seq<'a>, body:'a -> Async<unit>) : Async<unit> =
      async.For(sequence, body)
    member x.For (sequence:seq<'a>, body:'a -> Async<Result<unit, 'e list>>) : Async<Result<unit, 'e list>> =
      async {
        let errs = ResizeArray<_>()
        for a in sequence do
          let! r = body a
          match r with
          | Ok () -> ()
          | Error e -> errs.AddRange(e)
        if (errs.Count > 0) then return Error (errs |> List.ofSeq)
        else return Ok () }
    member x.While (guard:unit -> bool, computation:Async<unit>) : Async<unit> =
      async.While (guard, computation)
    member x.While (guard:unit -> bool, computation:Async<Result<unit, 'e list>>) : Async<Result<unit, 'e list>> =
      async {
        let errs = ResizeArray<_>()
        while guard () do
          let! r = computation
          match r with
          | Ok () -> ()
          | Error e -> errs.AddRange e
        if (errs.Count > 0) then return Error (errs |> List.ofSeq)
        else return Ok () }

  /// Async workflow builder for Async<Result<'a, 'e list>>
  let asyncResult = new AsyncResultBuilder ()

module AsyncLaws =

    /// Witness equality between two async computations.
    let private EQ (a:Async<'a>) (b:Async<'a>) = Async.RunSynchronously a = Async.RunSynchronously b

    /// Functor identity - mapping the identity function over an async computation leaves it unchaged.
    let identity (a:Async<'a>) = EQ (Async.map id a) (id a)

    /// Functor composition - composing functions outside of the functor is the same as composting them through the functor.
    let composition (a:Async<'a>) f g = EQ (Async.map (f << g) a) (Async.map f (Async.map g a))

    /// Monadic left unit - lifting a value into Async and binding to a function f is equivalent to applying f to the value.
    let leftUnit (a:'a) (f:'a -> Async<'b>) = EQ (async.Return a |> Async.bind f) (f a)

    /// Moandic right unit - binding an Async computation to a function which returns the underlying value leaves it unchanged.
    let rightUnit (aa:Async<'a>) = EQ (aa |> Async.bind (async.Return)) aa

    /// Monadic associative law - binding a computation to f and binding to result to g is the same as binding a computation to a function which applies f to the underlying value followed by a bind to g.
    /// In other words, binding first to f then to g is the same as binding to the result of composing f and g.
    let associativity (aa:Async<'a>) (f:'a -> Async<'b>) (g:'b -> Async<'c>) =
        EQ ((aa |> Async.bind f) |> Async.bind g) (aa |> Async.bind (fun a -> f a |> Async.bind g))
