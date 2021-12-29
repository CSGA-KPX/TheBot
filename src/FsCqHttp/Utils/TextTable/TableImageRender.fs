namespace rec KPX.FsCqHttp.Utils.TextResponse.Internals

open System
open System.Collections.Generic

open SkiaSharp

open KPX.FsCqHttp

open KPX.FsCqHttp.Utils.TextResponse


type internal TableBand() =
    let rowA = new SKPaint(Style = SKPaintStyle.Fill, Color = Config.ImageRowColorA)
    let rowB = new SKPaint(Style = SKPaintStyle.Fill, Color = Config.ImageRowColorB)

    let mutable status = false

    member x.BandPaint =
        status <- not status
        if status then rowA else rowB

type internal TableImageRender(table: IReadOnlyList<TableItem>, dParams: DrawParameters) =
    let (srcWidth, srcHeight) =
        let size: SKSize = dParams.Measure(table)
        (size.Width, size.Height)

    let surface =
        let actWidth = srcWidth * dParams.DrawScale
        let actHeight = srcHeight * dParams.DrawScale
        let ski = SKImageInfo(int actWidth, int actHeight, SKColorType.Rgba8888, SKAlphaType.Premul)

        SKSurface.Create(ski)

    let canvas = surface.Canvas

    let band = TableBand()

    let mutable pos = SKPoint()

    do canvas.Scale(dParams.DrawScale)

    member private x.UpdateLine(size: SKSize) = pos.Y <- pos.Y + size.Height

    member private x.DrawTable(table: IReadOnlyList<TableItem>) =
        let halfVerticalSpacing = dParams.RowVerticalSpacing / 2.0f

        for item in table do
            match item with
            | TableItem.Table t -> x.DrawTable(t)
            | TableItem.Line cell ->
                // Measure隐含ApplyPaint
                let textRect = dParams.Measure(cell)
                let rowSize = SKSize(srcWidth, textRect.Height + dParams.RowVerticalSpacing)

                canvas.DrawRect(SKRect.Create(pos, rowSize), band.BandPaint)

                let textPos = SKPoint(pos.X - textRect.Left, pos.Y - textRect.Top + halfVerticalSpacing)

                if cell.Align = TextAlignment.Right then
                    let rightPos = SKPoint(srcWidth, textPos.Y)
                    canvas.DrawText(cell.Text, rightPos, dParams.Paint)
                else
                    canvas.DrawText(cell.Text, textPos, dParams.Paint)

                // 更新坐标
                x.UpdateLine(rowSize)

            | TableItem.Row cols ->
                // 因为内部MeasureText测量算法不对劲，所以写死行高为最大行高
                let rowSize = SKSize(srcWidth, dParams.RowHeight + dParams.RowVerticalSpacing)
                canvas.DrawRect(SKRect.Create(pos, rowSize), band.BandPaint)

                let padOffset = 4.0f * dParams.SingleWidth

                let rowPos =
                    let mutable sum = 0f

                    dParams.MeasureMaxColumnWidth(table)
                    |> Array.map
                        (fun x ->
                            sum <- sum + x + padOffset
                            sum)

                let textRects = cols |> Array.map dParams.Measure

                let baseLine =
                    textRects
                    |> Seq.map (fun col -> pos.Y - col.Top + halfVerticalSpacing)
                    |> Seq.max

                for i = 0 to cols.Length - 1 do
                    let col = cols.[i]
                    let rect = textRects.[i]

                    col.ApplyPaint(dParams.Paint)

                    let x =
                        if col.Align = TextAlignment.Left then
                            (if i = 0 then 0f else rowPos.[i - 1]) - rect.Left
                        else
                            rowPos.[i] - padOffset

                    canvas.DrawText(col.Text, SKPoint(x, baseLine), dParams.Paint)

                x.UpdateLine(rowSize)

    member x.RenderImage() =
        canvas.Clear(SKColors.White)
        x.DrawTable(table)
        surface.Snapshot()

    interface IDisposable with
        member x.Dispose() =
            canvas.Dispose()
            surface.Dispose()
