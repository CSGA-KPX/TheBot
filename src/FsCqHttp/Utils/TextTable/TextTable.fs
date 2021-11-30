namespace rec KPX.FsCqHttp.Utils.TextResponse

open System
open System.Collections.Generic
open System.Text

open SkiaSharp

open KPX.FsCqHttp


type TableDrawContext(srcWidth: float32, srcHeight: float32, drawScale: float32) =
    static let rowA = new SKPaint(Style = SKPaintStyle.Fill, Color = SKColors.White)

    static let rowB = new SKPaint(Style = SKPaintStyle.Fill, Color = SKColors.LightGray)

    let mutable bandStatus = false

    let width = srcWidth * drawScale

    let height = srcHeight * drawScale

    let surface =
        let ski = SKImageInfo(int width, int height, SKColorType.Rgba8888, SKAlphaType.Premul)

        SKSurface.Create(ski)

    let canvas = surface.Canvas

    let mutable pos = SKPoint()

    do
        canvas.Scale(drawScale)
        canvas.Clear(SKColors.White)

    member x.Position = pos

    member x.DrawScale = drawScale

    member x.SrcWidth = srcWidth

    member x.SrcHeight = srcHeight

    member x.WidthScaled = width

    member x.HeightScaled = height

    member x.Canvas = canvas

    member x.GetBandColor() =
        bandStatus <- not bandStatus
        if bandStatus then rowA else rowB

    member x.GetImage() = surface.Snapshot()

    member x.UpdateLine(size: SKSize) = pos.Y <- pos.Y + size.Height

    member x.UpdateLine() = pos.X <- 0f

    member x.UpdateCol(size: SKSize) =
        pos.X <- pos.X + size.Width
        pos.Y <- pos.Y + size.Height

    interface IDisposable with
        member x.Dispose() =
            canvas.Dispose()
            surface.Dispose()

[<RequireQualifiedAccess>]
[<Struct>]
type TableItem =
    | Row of cols: TableCell []
    | Line of value: TableCell
    | Table of table: TextTable

type TextTable(?retType: ResponseType) =
    static let ColumnPadding = " \u3000 "
    static let FullWidthSpace = '\u3000'

    let items = ResizeArray<TableItem>()

    let skp = SkiaHelper.getPaint ()

    member val PreferResponseType = defaultArg retType PreferImage with get, set

    member x.Clear() = items.Clear()

    member x.Count = items.Count

    member x.GetImage() =
        let size: SKSize = x.CalculateImageSize()

        use ctx = new TableDrawContext(size.Width, size.Height, SkiaHelper.drawScale)

        x.DrawImage(ctx)
        ctx.GetImage()

    member internal x.DrawImage(ctx: TableDrawContext) =
        for item in items do
            match item with
            | TableItem.Table t -> t.DrawImage(ctx)
            | TableItem.Line cell ->
                /// 如果是空的，替换
                let cell =
                    if String.IsNullOrWhiteSpace(cell.Text) then
                        TableCell.Empty
                    else
                        cell

                cell.ApplyPaint(skp)

                let rowSize = SKSize(ctx.SrcWidth, cell.RectHeight + SkiaHelper.rowSpacing)

                ctx.Canvas.DrawRect(SKRect.Create(ctx.Position, rowSize), ctx.GetBandColor())

                let textPos =
                    SKPoint(
                        ctx.Position.X - cell.RectLeft,
                        ctx.Position.Y - cell.RectTop + SkiaHelper.rowSpacing / 2.0f
                    )

                if cell.Align = TextAlignment.Right then
                    let rightPos = SKPoint(ctx.SrcWidth, textPos.Y)
                    ctx.Canvas.DrawText(cell.Text, rightPos, skp)
                else
                    ctx.Canvas.DrawText(cell.Text, textPos, skp)

                // 更新坐标
                ctx.UpdateLine(rowSize)
            | TableItem.Row cols ->
                let cols =
                    cols
                    |> Array.map
                        (fun cell ->
                            /// 如果是空的，替换
                            if String.IsNullOrWhiteSpace(cell.Text) then
                                TableCell.Empty
                            else
                                cell)

                let rowHeight =
                    (cols |> Array.maxBy (fun col -> col.RectHeight))
                        .RectHeight
                    + SkiaHelper.rowSpacing

                let rowSize = SKSize(ctx.SrcWidth, rowHeight)
                ctx.Canvas.DrawRect(SKRect.Create(ctx.Position, rowSize), ctx.GetBandColor())

                let padOffset = 4.0f * SkiaHelper.chrSizeInfo.SingleWidth

                let rowPos =
                    let mutable sum = 0f

                    x.GetMaxColumnWidth()
                    |> Array.map
                        (fun x ->
                            sum <- sum + x + padOffset
                            sum)

                cols
                |> Array.iteri
                    (fun i col ->
                        col.ApplyPaint(skp)

                        let x =
                            if col.Align = TextAlignment.Left then
                                (if i = 0 then 0f else rowPos.[i - 1]) - col.RectLeft
                            else
                                rowPos.[i] - padOffset

                        let y = ctx.Position.Y - col.RectTop + SkiaHelper.rowSpacing / 2.0f

                        ctx.Canvas.DrawText(col.Text, SKPoint(x, y), skp))

                ctx.UpdateLine(rowSize)



    member x.CalculateImageSize() =
        let mutable size = SKSize()
        let lines = ref 0

        for item in items do
            match item with
            | TableItem.Row cols ->
                incr lines

                let maxHeight =
                    max
                        (cols |> Array.maxBy (fun col -> col.RectHeight))
                            .RectHeight
                        SkiaHelper.chrSizeInfo.Height

                size.Height <- size.Height + maxHeight
            // 不计算宽度，用x.GetMaxColumnWidth()求解。
            | TableItem.Line cell ->
                incr lines

                size.Width <- max size.Width cell.RectWidth

                size.Height <- size.Height + (max cell.RectHeight SkiaHelper.chrSizeInfo.Height)
            | TableItem.Table t ->
                let box = t.CalculateImageSize()
                size.Width <- max size.Width box.Width
                size.Height <- size.Height + box.Height

        let maxRowSize =
            let sizeInfo = x.GetMaxColumnWidth()
            let rowSize = sizeInfo |> Array.sum

            let padSize = (sizeInfo.Length - 1 |> float32) * 4.0f * SkiaHelper.chrSizeInfo.SingleWidth

            rowSize + padSize

        size.Width <- max size.Width maxRowSize


        size.Height <- size.Height + SkiaHelper.rowSpacing * (float32 !lines)

        size











    member x.GetLines() : IReadOnlyList<string> =
        let ret = ResizeArray<string>()
        let sb = StringBuilder()
        let maxCols: float32 [] = x.GetMaxColumnWidth()

        for item in items do
            match item with
            | TableItem.Row cols ->
                sb.Clear() |> ignore

                for i = 0 to cols.Length - 1 do
                    if i <> 0 then
                        sb.Append(ColumnPadding) |> ignore

                    let col = cols.[i]

                    let paddingFull, paddingHalf =
                        let paddingCols = (maxCols.[i] - col.RectWidth) / SkiaHelper.chrSizeInfo.SingleWidth |> int

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
            | TableItem.Table t -> ret.AddRange(t.GetLines())

        (ret :> IReadOnlyList<string>)

    member x.GetText() = String.Join("\r\n", x.GetLines())






    member private x.GetMaxColumnWidth() =
        let rows =
            items
            |> Seq.choose
                (fun item ->
                    match item with
                    | TableItem.Row cols -> Some cols
                    | _ -> None)
            |> Seq.cache

        if Seq.length rows = 0 then
            Array.empty<float32>
        else
            let colWidths =
                rows
                |> Seq.map (fun cols -> cols.Length)
                |> Seq.max
                |> Array.zeroCreate<float32>

            for row in rows do
                for i = 0 to row.Length - 1 do
                    let col = row.[i]
                    colWidths.[i] <- max colWidths.[i] col.RectWidth

            colWidths


















    member x.Yield(line: string) =
        // Line
        if line.Contains('\n') then
            invalidOp "TableCell不允许包含多行文本，请使用相关指令拆分"

        items.Add(TableItem.Line <| TableCell(line, skp))
        x

    member x.Yield(cell: CellBuilder) =
        // Line
        cell.ToTableCells(skp) |> Seq.map TableItem.Line |> items.AddRange

        x

    member x.YieldFrom(rows: seq<CellBuilder>) =
        for row in rows do
            row.ToTableCells(skp) |> Array.map TableItem.Line |> items.AddRange

        x

    member x.Yield(row: seq<CellBuilder>) =
        row
        |> Seq.map
            (fun c ->
                let cell = c.ToTableCells(skp)

                if cell.Length <> 1 then
                    invalidOp $"row模式下Cell必须有且只有一段内容，当前有%A{c.Content}"

                cell |> Array.head)
        |> Seq.toArray
        |> TableItem.Row
        |> items.Add

        x

    member x.Yield(rows: seq<seq<CellBuilder>>) =
        for row in rows do
            x.Yield(row) |> ignore

        x

    member x.Yield(rows: seq<CellBuilder list>) =
        for row in rows do
            x.Yield(row) |> ignore

        x

    member x.Yield(rows: seq<CellBuilder []>) =
        for row in rows do
            x.Yield(row) |> ignore

        x

    member x.Yield(table: TextTable) =
        items.Add(TableItem.Table table)
        x

    member x.Yield(_: unit) = x

    member x.Zero() = x

    member x.Combine(_, _) = x

    member x.Delay(func) = func ()

    (*
    // 原因不明用不了
    [<CustomOperation("forceText")>]
    member _.ForceText(x : TextTable) =
        x.PreferResponseType <- ForceText
        x

    [<CustomOperation("preferImage")>]
    member _.PreferImage(x : TextTable) =
        x.PreferResponseType <- PreferImage
        x

    [<CustomOperation("forceImage")>]
    member _.ForceImage(x : TextTable) =
        x.PreferResponseType <- ForceImage
        x
    *)

    member x.Response(args) =
        (x :> KPX.FsCqHttp.Handler.ICommandResponse)
            .Response(args)

    interface KPX.FsCqHttp.Handler.ICommandResponse with
        member x.Response(args) =
            let canSendImage =
                lazy
                    (args
                        .ApiCaller
                        .CallApi<Api.System.CanSendImage>()
                        .Can)

            let returnImage =
                match x.PreferResponseType with
                | ForceText -> false
                | PreferImage when Config.ImageIgnoreSendCheck -> true
                | PreferImage -> canSendImage.Force()
                | ForceImage -> true

            if x.Count <> 0 then
                if returnImage then
                    let message = Message.Message()
                    message.Add(x.GetImage())
                    args.Reply(message)
                else
                    let sizeLimit = Config.TextLengthLimit - 100
                    let sb = StringBuilder()

                    for line in x.GetLines() do
                        sb.AppendLine(line) |> ignore

                        if sb.Length > sizeLimit then
                            // 删除结尾换行符
                            sb.Length <- sb.Length - Environment.NewLine.Length
                            args.Reply(sb.ToString())
                            sb.Clear() |> ignore

                    if sb.Length <> 0 then
                        // 删除结尾换行符
                        sb.Length <- sb.Length - Environment.NewLine.Length
                        args.Reply(sb.ToString())
