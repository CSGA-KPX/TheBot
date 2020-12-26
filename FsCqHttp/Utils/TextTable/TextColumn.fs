namespace KPX.FsCqHttp.Utils.TextTable

open System
open System.Collections.Generic


/// 表示表格内每列的内容
type internal TextColumn() =
    inherit List<TableCell>()

    let mutable defaultLeftAlignment = true

    /// 设置默认为左对齐
    member x.SetLeftAlignment() = defaultLeftAlignment <- true

    /// 设置默认为右对齐
    member x.SetRightAlignment() = defaultLeftAlignment <- false

    /// 添加为默认对齐方式
    member x.AddDefaultAlignment(o : obj) =
        let add =
            if defaultLeftAlignment then TableCell.CreateLeftAlign(o) else TableCell.CreateRightAlign(o)

        x.Add(add)

    /// 将列内所有单元格重置为左对齐
    member x.ForceLeftAlign() =
        x.SetLeftAlignment()

        for i = 0 to x.Count - 1 do
            x.[i] <- LeftAlignCell x.[i].Value

    /// 将列内所有单元格重置为右对齐
    member x.ForceRightAlign() =
        x.SetRightAlignment()

        for i = 0 to x.Count - 1 do
            x.[i] <- RightAlignCell x.[i].Value

    member x.GetMaxDisplayWidth() =
        x
        |> Seq.map (fun cell -> cell.DisplayWidth)
        |> Seq.max

    /// 对齐到指定大小
    member x.DoAlignment(padChar : char) =
        let max = x.GetMaxDisplayWidth()

        let padCharLen =
            KPX.FsCqHttp.Config.Output.TextTable.CharLen(padChar)

        for i = 0 to x.Count - 1 do
            let cell = x.[i]
            let width = cell.DisplayWidth
            let padLen = (max - width) / padCharLen // 整数部分用padChar补齐
            let rstLen = (max - width) % padCharLen // 非整数部分用空格补齐

            if padLen <> 0 || rstLen <> 0 then
                let padding =
                    String(padChar, padLen) + String(' ', rstLen)

                x.[i] <- if cell.IsLeftAlign then
                             LeftAlignCell(cell.Value + padding)
                         else
                             RightAlignCell(padding + cell.Value)
