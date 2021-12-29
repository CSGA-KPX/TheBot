namespace rec KPX.FsCqHttp.Utils.TextResponse.Internals

open System
open System.Collections.Generic

open SkiaSharp

open KPX.FsCqHttp

open KPX.FsCqHttp.Utils.TextResponse


type DrawParameters() =
    let fontSize = Config.ImageOutputSize

    let familyName = Config.ImageOutputFont

    let typeface = (familyName, SKFontStyle.Normal) |> SKFontManager.Default.MatchFamily

    let fontRegular = new SKFont(typeface, fontSize)

    let paint =
        new SKPaint(
            fontRegular,
            IsAutohinted = true,
            IsAntialias = true,
            Color = SKColors.Black,
            IsStroke = false,
            TextAlign = SKTextAlign.Left,
            FakeBoldText = false
        )

    let mutable rect = SKRect()

    let rowHeight =
        paint.MeasureText("---qwertyuiopasdfghjklzxcvbnm\u5BBD", &rect) |> ignore
        rect.Height

    member val SingleWidth =
        let width = paint.MeasureText("\u5BBD", &rect)
        width / 2.0f

    member val RowVerticalSpacing =
        let space = rowHeight * 0.10f |> ceil
        space + 0.5f |> round // 保证能被2整除

    member x.Paint = paint

    member x.RowHeight = rowHeight

    member x.DrawScale = 1.5f

    member x.Measure(cell: TableCell) =
        if String.IsNullOrEmpty(cell.Text) then
            SKRect.Empty
        else
            cell.ApplyPaint(paint)
            let width = paint.MeasureText(cell.Text, &rect)

            // MeasureText中rect不考虑空白字符
            // 如果有任何字符，保证高度为一个汉字高度
            // max x.RowHeight rect.Height有问题，descenders之类的会比定义的RowHeight高
            // SKRect.Create(rect.Left, rect.Top, width, max x.RowHeight rect.Height)
            SKRect.Create(rect.Left, rect.Top, width, x.RowHeight)

    member x.Measure(table: IReadOnlyList<TableItem>) : SKSize =
        let mutable size = SKSize()
        let mutable lines = 0
        let rowWidth = ResizeArray<float32>(16) // 绝大部分情况下不会超过16列

        for item in table do
            match item with
            | TableItem.Line cell ->
                lines <- lines + 1

                let rect = x.Measure(cell)
                size.Width <- max size.Width rect.Width
                size.Height <- size.Height + (max rowHeight rect.Height)

            | TableItem.Row cols ->
                lines <- lines + 1

                let colSizes = cols |> Array.map (x.Measure)
                rowWidth.EnsureCapacity(cols.Length) |> ignore

                let highestItem = colSizes |> Array.maxBy (fun x -> x.Height)
                size.Height <- size.Height + highestItem.Height

                for i = 0 to colSizes.Length - 1 do
                    if i + 1 > rowWidth.Count then
                        rowWidth.Add(colSizes.[i].Width)
                    else
                        rowWidth.[i] <- max rowWidth.[i] colSizes.[i].Width

            | TableItem.Table t ->
                let tableSize = x.Measure(t)
                size.Width <- max size.Width tableSize.Width
                size.Height <- size.Height + tableSize.Height

        let maxRowWidth =
            let textWidth = rowWidth |> Seq.sum
            let padSize = (rowWidth.Count - 1 |> float32) * 4.0f * x.SingleWidth
            textWidth + padSize

        size.Width <- max size.Width maxRowWidth
        size.Height <- size.Height + x.RowVerticalSpacing * (float32 lines)

        size

    member x.MeasureMaxColumnWidth(rows: seq<TableCell []>) =
        if Seq.isEmpty rows then
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
                    colWidths.[i] <- max colWidths.[i] (x.Measure(col).Width)

            colWidths

    member x.MeasureMaxColumnWidth(table: IReadOnlyList<TableItem>) =
        table
        |> Seq.choose
            (fun item ->
                match item with
                | TableItem.Row cols -> Some cols
                | _ -> None)
        |> x.MeasureMaxColumnWidth

    interface IDisposable with
        member x.Dispose() =
            paint.Dispose()
            typeface.Dispose()
            fontRegular.Dispose()
            GC.SuppressFinalize(x)

    // TextTable依赖DrawParameters
    // 但是TextTable不能被定义为IDisposable
    // 只能改写析构函数
    override x.Finalize() =
        paint.Dispose()
        typeface.Dispose()
        fontRegular.Dispose()