[<AutoOpen>]
module KPX.FsCqHttp.Utils.TextTable.Implementation

open System
open System.Collections.Generic


/// 延迟TextTable的求值时间点，便于在最终输出前对TextTable的参数进行调整
type private DelayedTableItem =
    | StringItem of string
    | TableItem of TextTable

and TextTable([<ParamArray>] header : Object []) =
    static let builder = RowBuilder()

    let preTableLines = List<DelayedTableItem>()
    let postTableLines = List<DelayedTableItem>()

    let colCount = header.Length
    let cols = List<TextColumn>(colCount)

    do
        for _ = 0 to colCount - 1 do
            cols.Add(TextColumn())

        header
        |> Array.iteri
            (fun i o ->
                let cell = TableCell.CreateFrom(o)
                cols.[i].Add(cell)
                // 根据表头设置默认的对齐类型
                if cell.IsLeftAlign then
                    cols.[i].SetLeftAlignment()
                else
                    cols.[i].SetRightAlignment())

    member val ColumnPaddingChar = KPX.FsCqHttp.Config.Output.TextTable.FullWidthSpace with get, set

    member x.AddPreTable(str : string) = preTableLines.Add(StringItem str)
    member x.AddPreTable(tt : TextTable) = preTableLines.Add(TableItem tt)

    member x.AddPostTable(str : string) = postTableLines.Add(StringItem str)
    member x.AddPostTable(tt : TextTable) = postTableLines.Add(TableItem tt)

    /// 获取用于行构造器的对象
    member x.RowBuilder = builder

    member x.AddRow(builderType : RowBuilderType) =
        let (B builder) = builderType
        let fields = List<_>()
        builder fields

        if fields.Count <> colCount
        then invalidArg (nameof builder) (sprintf "数量不足：需求%i 提供%i" colCount fields.Count)

        fields |> Seq.iteri (fun i c -> cols.[i].Add(c))

    member x.AddRow([<ParamArray>] objs : Object []) =
        if objs.Length <> colCount
        then invalidArg (nameof objs) (sprintf "数量不足：需求%i 提供%i" colCount objs.Length)

        objs
        |> Array.iteri (fun i o -> cols.[i].Add(TableCell.CreateFrom(o)))

    /// 使用指定字符串填充未使用的单元格
    member x.AddRowFill([<ParamArray>] objs : Object []) =
        if objs.Length > colCount
        then invalidArg (nameof objs) (sprintf "输入过多：需求%i 提供%i" colCount objs.Length)

        for i = 0 to objs.Length - 1 do
            cols.[i].Add(TableCell.CreateFrom(objs.[i]))

        let def =
            box KPX.FsCqHttp.Config.Output.TextTable.CellPadding
        for i = objs.Length to colCount - 1 do
            cols.[i].AddDefaultAlignment(def)

    member x.ToLines() =
        [| let expand (l : List<DelayedTableItem>) =
            seq {
                for item in l do
                    match item with
                    | StringItem str -> yield str
                    | TableItem tt ->
                        tt.ColumnPaddingChar <- x.ColumnPaddingChar
                        yield! tt.ToLines()
            }

           yield! expand preTableLines

           for col in cols do
               col.DoAlignment(x.ColumnPaddingChar)

           let interColumnPadding = sprintf " %c " x.ColumnPaddingChar
           let sb = Text.StringBuilder()
           for row = 0 to cols.[0].Count - 1 do
               sb.Clear().Append(cols.[0].[row].Value) |> ignore
               for col = 1 to colCount - 1 do
                   sb
                       .Append(interColumnPadding)
                       .Append(cols.[col].[row].Value)
                   |> ignore

               yield sb.ToString()

           yield! expand postTableLines |]


    override x.ToString() = String.Join("\r\n", x.ToLines())

type KPX.FsCqHttp.Utils.TextResponse.TextResponse with
    member x.Write(tt : TextTable) =
        if x.DoSendImage then tt.ColumnPaddingChar <- ' '

        for line in tt.ToLines() do
            x.WriteLine(line)
