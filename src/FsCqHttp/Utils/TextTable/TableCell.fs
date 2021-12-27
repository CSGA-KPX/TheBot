namespace rec KPX.FsCqHttp.Utils.TextResponse

open System

open KPX.FsCqHttp

open SkiaSharp


[<Struct>]
[<RequireQualifiedAccess>]
type TextAlignment =
    | Left
    | Right

type TableCell(content: string, dParams: DrawParameters) =

    let rect: SKRect = dParams.MeasureText(content)

    member val Align = TextAlignment.Left with get, set

    member val FakeBold = false with get, set

    member val TextColor = SKColors.Black with get, set

    member x.Text = content

    member x.RectTop = rect.Top

    member x.RectLeft = rect.Left

    member x.RectWidth = rect.Width

    member x.RectHeight = rect.Height

    member x.ApplyPaint(skp: SKPaint) =
        match x.Align with
        | TextAlignment.Left -> skp.TextAlign <- SKTextAlign.Left
        | TextAlignment.Right -> skp.TextAlign <- SKTextAlign.Right

        skp.FakeBoldText <- x.FakeBold
        skp.Color <- x.TextColor

type DrawParameters() as x =

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

    let rowHeight =
        let mutable rect = SKRect()
        paint.MeasureText("---qwertyuiopasdfghjklzxcvbnm\u5BBD", &rect) |> ignore
        rect.Height

    let emptyCell = lazy (TableCell("X", x, TextColor = SKColor.Parse("00FFFFFF")))

    member val SingleWidth =
        let mutable rect = SKRect()
        let width = paint.MeasureText("\u5BBD", &rect)
        width / 2.0f

    member val RowVerticalSpacing =
        let space = rowHeight * 0.10f |> ceil
        space + 0.5f |> round // 保证能被2整除

    member x.Paint = paint

    member x.RowHeight = rowHeight

    member x.RowHorizontalSpacing = 10.0f

    member x.DrawScale = 1.5f

    member x.MeasureText(str: string) =
        if String.IsNullOrEmpty(str) then
            SKRect.Empty
        else
            let mutable rect = SKRect()
            let width = paint.MeasureText(str, &rect)

            // 警告：MeasureText中rect不考虑空白字符
            // 如果有任何字符，保证高度为一个汉字高度
            //SKRect.Create(rect.Left, rect.Top, width, max x.RowHeight rect.Height)
            SKRect.Create(rect.Left, rect.Top, width, x.RowHeight)

    member x.EmptyTableCell = emptyCell.Force()
