namespace KPX.FsCqHttp.Utils.TextTable

open System.Collections.Generic


[<Struct>]
type RowBuilderType = internal B of (List<TableCell> -> unit)

/// 行生成器，方便复杂表生成行
type RowBuilder internal () =
    let (!) =
        function
        | B f -> f

    member _.Yield(value : obj) =
        B(fun b -> b.Add(TableCell.CreateFrom(value)))

    member _.Combine(f, g) =
        B
            (fun b ->
                ! f b
                ! g b)

    member _.Delay f = B(fun b -> ! (f ()) b)
    member _.Zero() = B(fun _ -> ())

    member _.For(xs : 'a seq, f : 'a -> RowBuilderType) =
        B
            (fun b ->
                let e = xs.GetEnumerator()

                while e.MoveNext() do
                    ! (f e.Current) b)

    member _.While(p : unit -> bool, f : RowBuilderType) =
        B
            (fun b ->
                while p () do
                    ! f b)
