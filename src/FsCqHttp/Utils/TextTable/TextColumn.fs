namespace KPX.FsCqHttp.Utils.TextTable

open System.Collections.Generic

open KPX.FsCqHttp.Utils.TextResponse


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
            if defaultLeftAlignment then
                LeftAlignCell o
            else
                RightAlignCell o

        x.Add(add)

    /// 将列内所有单元格重置为左对齐
    member x.ForceLeftAlign() =
        x.SetLeftAlignment()

        for i = 0 to x.Count - 1 do
            x.[i] <- LeftAlignCell x.[i].Text

    /// 将列内所有单元格重置为右对齐
    member x.ForceRightAlign() =
        x.SetRightAlignment()

        for i = 0 to x.Count - 1 do
            x.[i] <- RightAlignCell x.[i].Text

    member x.GetMaxDisplayWidth(measurer : ImageMeasurer) =
        x
        |> Seq.map (fun cell -> cell.DisplayWidthOf(measurer))
        |> Seq.max