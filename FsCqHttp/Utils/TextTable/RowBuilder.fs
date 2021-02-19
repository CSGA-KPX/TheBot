namespace KPX.FsCqHttp.Utils.TextTable

open System


type RowBuilder private () =
    member _.Bind(m : seq<_>, f : 'a -> seq<_>) = m |> Seq.collect f

    member _.Delay(f : unit -> seq<_>) = f ()

    // member _.Return

    // member _.ReturnFrom

    // member _.Run (f : unit -> seq<_>) = f()

    member _.Combine(xs1 : seq<_>, xs2 : seq<_>) = Seq.append xs1 xs2

    member _.For(xs : seq<_>, f : _ -> seq<_>) =
        seq {
            for item in xs do
                yield! f (item)
        }

    member _.TryFinally(body, final : unit -> unit) =
        seq {
            try
                yield! body ()
            finally
                final ()
        }

    member x.Using(value : 'a, body : 'a -> seq<_> when 'a :> IDisposable) =
        let tempBody = fun () -> body value

        let dispose =
            fun () ->
                match value with
                | null -> ()
                | _ -> value.Dispose()

        x.TryFinally(tempBody, dispose)

    member _.While(guard : unit -> bool, xs : seq<_>) = Seq.takeWhile guard xs

    // member _.Yield

    // member _.YieldFrom

    member _.Zero() = Seq.empty

    member _.Yield(value : obj) =
        Seq.singleton <| TableCell.CreateFrom(value)

    static member val Instance = RowBuilder()
