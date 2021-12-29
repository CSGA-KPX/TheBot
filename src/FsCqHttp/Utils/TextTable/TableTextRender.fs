namespace rec KPX.FsCqHttp.Utils.TextResponse.Internals

open System
open System.Collections.Generic
open System.Text

open KPX.FsCqHttp.Utils.TextResponse


type internal TableTextRender(table: IReadOnlyList<TableItem>, dParams: DrawParameters) =
    static let ColumnPadding = " \u3000 "
    static let FullWidthSpace = '\u3000'

    member x.GetLines() : IReadOnlyList<string> =
        let ret = ResizeArray<string>()
        let sb = StringBuilder()

        let maxCols: float32 [] =
            table
            |> Seq.choose
                (fun item ->
                    match item with
                    | TableItem.Row cols -> Some cols
                    | _ -> None)
            |> dParams.MeasureMaxColumnWidth

        for item in table do
            match item with
            | TableItem.Row cols ->
                sb.Clear() |> ignore

                for i = 0 to cols.Length - 1 do
                    if i <> 0 then
                        sb.Append(ColumnPadding) |> ignore

                    let col = cols.[i]

                    let paddingFull, paddingHalf =
                        let rect = dParams.Measure(col)
                        let paddingCols = (maxCols.[i] - rect.Width) / dParams.SingleWidth |> int

                        Math.DivRem(paddingCols, 2)

                    // 因为已经约束，Row类型内不得使用多行文本
                    match col.Align with
                    | TextAlignment.Left ->
                        sb
                            .Append(col.Text)
                            .Append(FullWidthSpace, paddingFull)
                            .Append(' ', paddingHalf)
                        |> ignore
                    | TextAlignment.Right ->
                        sb
                            .Append(FullWidthSpace, paddingFull)
                            .Append(' ', paddingHalf)
                            .Append(col.Text)
                        |> ignore

                ret.Add(sb.ToString())
            | TableItem.Line cell ->
                // 文本输出模式下，不考虑右对齐
                ret.Add(cell.Text)
            | TableItem.Table items -> ret.AddRange(TableTextRender(items, dParams).GetLines())

        (ret :> IReadOnlyList<string>)

    member x.GetText() = String.Join("\r\n", x.GetLines())
