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

type TableCell private (content: string) =

    static let emptyCellContent = "--"

    static let emptyCellColor = SKColor.Parse("00FFFFFF")

    member val Align = TextAlignment.Left with get, set

    member val FakeBold = false with get, set

    member val TextColor = SKColors.Black with get, set

    member val RenderMode = RenderMode.Normal with get, set

    member x.Text = content

    /// <summary>
    /// 将当前TableCell的设定反映到给定SKPaint上
    /// </summary>
    /// <param name="skp">被更新的SKPaint</param>
    member x.ApplyPaint(skp: SKPaint) =
        match x.Align with
        | TextAlignment.Left -> skp.TextAlign <- SKTextAlign.Left
        | TextAlignment.Right -> skp.TextAlign <- SKTextAlign.Right

        skp.FakeBoldText <- x.FakeBold

        match x.RenderMode with
        | RenderMode.Normal -> skp.Color <- x.TextColor
        | RenderMode.IgnoreIfImage
        | RenderMode.IgnoreAll -> skp.Color <- emptyCellColor

    /// <summary>
    /// 根据内容创建TableCell
    ///
    /// 如果内容为空，返回TableCell.EmptyTableCell
    /// </summary>
    /// <param name="content"></param>
    static member Create(content: string) =
        if String.IsNullOrWhiteSpace(content) then
            TableCell(emptyCellContent)
        else
            TableCell(content)
