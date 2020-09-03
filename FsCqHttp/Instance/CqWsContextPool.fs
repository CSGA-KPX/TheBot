namespace KPX.FsCqHttp.Instance 

open System
open System.Collections.Concurrent

type CqWsContextPool private () = 
    static let instance = CqWsContextPool()

    let pool = ConcurrentDictionary<uint64, CqWsContext>()

    member x.AddContext(context : CqWsContext) = pool.TryAdd(context.Self.UserId, context) |> ignore

    member x.RemoveContext(context : CqWsContext) = pool.TryRemove(context.Self.UserId) |> ignore

    interface Collections.IEnumerable with
        member x.GetEnumerator() = pool.Values.GetEnumerator() :> Collections.IEnumerator

    interface Collections.Generic.IEnumerable<CqWsContext> with
        member x.GetEnumerator() = pool.Values.GetEnumerator()

    static member Instance = instance