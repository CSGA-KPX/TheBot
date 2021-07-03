﻿namespace rec KPX.FsCqHttp.Utils.TextTable

open System
open System.Collections.Generic

open System.Text
open KPX.FsCqHttp

open KPX.FsCqHttp.Utils.TextResponse


/// 延迟TextTable的求值时间点，便于在最终输出前对TextTable的参数进行调整
type private DelayedTableItem =
    | StringItem of string
    | TableItem of TextTable

type TextTable([<ParamArray>] header : Object []) =
    let preTableLines = List<DelayedTableItem>()
    let postTableLines = List<DelayedTableItem>()

    let colCount = header.Length
    let cols = List<TextColumn>(colCount)

    let measurer = ImageMeasurer()

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

    member val ColumnPaddingChar = Config.FullWidthSpace with get, set

    member x.AddPreTable(str : string) = preTableLines.Add(StringItem str)
    member x.AddPreTable(tt : TextTable) = preTableLines.Add(TableItem tt)

    member x.AddPostTable(str : string) = postTableLines.Add(StringItem str)
    member x.AddPostTable(tt : TextTable) = postTableLines.Add(TableItem tt)

    /// 获取用于行构造器的对象
    member x.RowBuilder = RowBuilder.Instance

    member x.AddRow(fields : IEnumerable<TableCell>) =
        let fCount = Seq.length fields

        if fCount <> colCount then
            invalidArg (nameof fields) $"数量不足：需求%i{colCount} 提供%i{fCount}"

        fields |> Seq.iteri (fun i -> cols.[i].Add)

    member x.AddRow([<ParamArray>] objs : Object []) =
        if objs.Length <> colCount then
            invalidArg (nameof objs) $"数量不足：需求%i{colCount} 提供%i{objs.Length}"

        objs
        |> Array.iteri (fun i o -> cols.[i].Add(TableCell.CreateFrom(o)))

    /// 使用指定字符串填充未使用的单元格
    member x.AddRowFill([<ParamArray>] objs : Object []) =
        if objs.Length > colCount then
            invalidArg (nameof objs) $"输入过多：需求%i{colCount} 提供%i{objs.Length}"

        for i = 0 to objs.Length - 1 do
            cols.[i].Add(TableCell.CreateFrom(objs.[i]))

        let def = box Config.TableCellPadding

        for i = objs.Length to colCount - 1 do
            cols.[i].AddDefaultAlignment(def)

    member x.ToLines() =
        let padCharLen =
            measurer.MeasureWidthByConfig(string x.ColumnPaddingChar)

        if padCharLen = 0 then
            invalidArg "ColumnPaddingChar" $"字符长度计算错误： %c{x.ColumnPaddingChar} 的栏位数为0"

        let expand (l : List<DelayedTableItem>) =
            seq {
                for item in l do
                    match item with
                    | StringItem str -> yield str
                    | TableItem tt ->
                        tt.ColumnPaddingChar <- x.ColumnPaddingChar
                        yield! tt.ToLines()
            }

        let writeCell (cell : TableCell) (targetLen : int) (out : StringBuilder) =
            let width = cell.DisplayWidthOf(measurer)
            let padLen = (targetLen - width) / padCharLen // 整数部分用padChar补齐
            let rstLen = (targetLen - width) % padCharLen // 非整数部分用空格补齐

            if padLen <> 0 || rstLen <> 0 then
                if cell.IsLeftAlign then
                    out
                        .Append(cell.Text)
                        .Append(x.ColumnPaddingChar, padLen)
                        .Append(' ', rstLen)
                    |> ignore
                else
                    out
                        .Append(x.ColumnPaddingChar, padLen)
                        .Append(' ', rstLen)
                        .Append(cell.Text)
                    |> ignore
            else
                out.Append(cell.Text) |> ignore

        [| yield! expand preTableLines

           // 每栏之间加入半角空格
           // 方便文本选取
           let interColumnPadding = $" %c{x.ColumnPaddingChar} "
           let sb = StringBuilder()

           let maxColLen =
               cols
               |> Seq.map (fun col -> col.GetMaxDisplayWidth(measurer))
               |> Seq.toArray

           for row = 0 to cols.[0].Count - 1 do
               writeCell cols.[0].[row] maxColLen.[0] sb

               for col = 1 to colCount - 1 do
                   sb.Append(interColumnPadding) |> ignore
                   writeCell cols.[col].[row] maxColLen.[col] sb

               yield sb.ToString()
               sb.Clear() |> ignore

           yield! expand postTableLines |]

    override x.ToString() =
        String.Join(Config.NewLine, x.ToLines())
