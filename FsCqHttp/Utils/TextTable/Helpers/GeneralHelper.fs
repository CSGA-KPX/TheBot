namespace KPX.FsCqHttp.Utils.TextTable

open System


[<Sealed>]
[<AutoOpen>]
type GeneralHelpers() =
    static let padLeft =
        LeftAlignCell KPX.FsCqHttp.Config.Output.TextTable.CellPadding

    static let padRight =
        RightAlignCell KPX.FsCqHttp.Config.Output.TextTable.CellPadding

    /// 预定义的左对齐填充单元格 Config.Output.TextTable.CellPadding
    static member PaddingLeft = padLeft

    /// 预定义的右对齐填充单元格 Config.Output.TextTable.CellPadding
    static member PaddingRight = padRight

    static member LeftAlignCell(value : obj) =
        TableCell.CreateFrom(value, CellAlign.Left)

    static member RightAlignCell(value : obj) =
        TableCell.CreateFrom(value, CellAlign.Right)

    static member LeftAlignCell(value : string) =
        { TableCell.Text = value
          TableCell.Align = CellAlign.Left }

    static member RightAlignCell(value : string) =
        { TableCell.Text = value
          TableCell.Align = CellAlign.Right }
