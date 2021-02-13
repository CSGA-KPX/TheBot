namespace KPX.FsCqHttp.Utils.TextTable


type RowBuilder private () =
    member _.Bind(m : seq<'a>, f : 'a -> seq<TableCell>) = m |> Seq.collect f

    member _.Zero() = Seq.empty

    member _.Yield(value : obj) =
        Seq.singleton <| TableCell.CreateFrom(value)

    member x.For(m, f) = x.Bind(m, f)

    member _.Combine(a, b) = b |> Seq.append a

    member _.Delay(f) = f ()

    static member val Instance = RowBuilder()