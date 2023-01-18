namespace rec KPX.FsCqHttp.Utils.TextResponse

open System
open System.Collections.Generic

open SkiaSharp


[<RequireQualifiedAccess>]
[<Struct>]
type TableItem =
    | Row of cols: TableCell []
    | Line of value: TableCell
    | Table of table: IReadOnlyList<TableItem>

[<RequireQualifiedAccess>]
[<Struct>]
type RenderMode =
    /// 正常绘制
    | Normal
    /// 在图片模式下使用透明色绘制，文本正常
    | IgnoreIfImage
    /// 在图片模式下使用透明色绘制，文本按无字符处理
    | IgnoreAll

module private TableCellUtils =
    let emptyCellContent = "--"
    let emptyCellColor = SKColor.Parse("00FFFFFF")

type TableCell = 
    val Text: string
    val mutable Align: TextAlignment
    val mutable Bold: bool
    val mutable TextColor: SKColor
    val mutable RenderMode: RenderMode

    new(content: string) =
        if content.Contains('\n') then
            invalidOp "TableCell不允许包含多行文本"

        { Text = if String.IsNullOrEmpty(content) then TableCellUtils.emptyCellContent else content
          Align = TextAlignment.Left
          Bold = false
          TextColor = SKColors.Black
          RenderMode =
            if String.IsNullOrEmpty(content) then
                RenderMode.IgnoreIfImage
            else
                RenderMode.Normal }
    /// <summary>
    /// 将当前TableCell的设定反映到给定SKPaint上
    /// </summary>
    /// <param name="skp">被更新的SKPaint</param>
    member x.ApplyPaint(skp: SKPaint) =
        match x.Align with
        | TextAlignment.Left -> skp.TextAlign <- SKTextAlign.Left
        | TextAlignment.Right -> skp.TextAlign <- SKTextAlign.Right

        skp.FakeBoldText <- x.Bold

        match x.RenderMode with
        | RenderMode.Normal -> skp.Color <- x.TextColor
        | RenderMode.IgnoreIfImage
        | RenderMode.IgnoreAll -> skp.Color <- TableCellUtils.emptyCellColor

    // if/else/for内不能使用自定义操作符
    // 所以不实现if/for等指令

    member x.Yield _ = x

    member x.Zero() = x

    [<CustomOperation("leftAlign")>]
    member inline x.LeftAlign(_: TableCell) =
        x.Align <- TextAlignment.Left
        x

    [<CustomOperation("rightAlign")>]
    member inline x.RightAlign(_: TableCell) =
        x.Align <- TextAlignment.Right
        x

    [<CustomOperation("bold")>]
    member inline x.SetBold(_: TableCell) =
        x.Bold <- true
        x

    [<CustomOperation("hide")>]
    member inline x.Hide(_: TableCell) =
        x.RenderMode <- RenderMode.IgnoreIfImage
        x

    [<CustomOperation("leftAlignIf")>]
    member inline x.LeftAlign(_: TableCell, cond: bool) =
        if cond then
            x.Align <- TextAlignment.Left

        x

    [<CustomOperation("rightAlignIf")>]
    member inline x.RightAlign(_: TableCell, cond: bool) =
        if cond then
            x.Align <- TextAlignment.Right

        x

    [<CustomOperation("boldIf")>]
    member inline x.SetBold(_: TableCell, cond: bool) =
        if cond then x.Bold <- true
        x

    [<CustomOperation("hideIf")>]
    member inline x.Hide(_: TableCell, cond: bool) =
        if cond then
            x.RenderMode <- RenderMode.IgnoreIfImage

        x

    [<CustomOperation("textWhite")>]
    member inline x.SetTextWhite(_: TableCell) =
        x.TextColor <- SKColors.White
        x

    [<CustomOperation("textBlack")>]
    member inline x.SetTextBlack(_: TableCell) =
        x.TextColor <- SKColors.Black
        x

    [<CustomOperation("textRed")>]
    member inline x.SetTextRed(_: TableCell) =
        x.TextColor <- SKColors.Red
        x

    [<CustomOperation("textWhiteIf")>]
    member inline x.SetTextWhite(_: TableCell, cond: bool) =
        if cond then
            x.TextColor <- SKColors.White

        x

    [<CustomOperation("textBlackIf")>]
    member inline x.SetTextBlack(_: TableCell, cond: bool) =
        if cond then
            x.TextColor <- SKColors.Black

        x

    [<CustomOperation("textRedIf")>]
    member inline x.SetTextRed(_: TableCell, cond: bool) =
        if cond then x.TextColor <- SKColors.Red
        x
