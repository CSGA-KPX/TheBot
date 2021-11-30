namespace rec KPX.FsCqHttp.Utils.TextResponse

open SkiaSharp


[<Struct>]
[<RequireQualifiedAccess>]
type TextAlignment =
    | Left
    | Right

type TableCell(content: string, skp: SKPaint) =

    let rect: SKRect = SkiaHelper.measureText (content, skp)

    static member val Empty =
        let skp = SkiaHelper.getPaint ()
        TableCell("X", skp, TextColor = SKColor.Parse("00FFFFFF"))

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
