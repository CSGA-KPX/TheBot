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

type TableCell private (content: string) =

    static let emptyCellContent = "--"

    member val Align = TextAlignment.Left with get, set

    member val FakeBold = false with get, set

    member val TextColor = SKColors.Black with get, set

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
        skp.Color <- x.TextColor

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