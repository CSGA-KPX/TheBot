module KPX.FsCqHttp.Utils.TextResponse.SkiaHelper

open System
open SkiaSharp
open KPX.FsCqHttp


// 因为Bold和非Bold的宽度不一样，只能用FakeBold了

let fontSize = Config.ImageOutputSize

let familyName = Config.ImageOutputFont

let typeface = (familyName, SKFontStyle.Normal) |> SKFontManager.Default.MatchFamily

let fontRegular = new SKFont(typeface, fontSize)

let getPaint () =
    new SKPaint(
        fontRegular,
        IsAutohinted = true,
        IsAntialias = true,
        Color = SKColors.Black,
        IsStroke = false,
        TextAlign = SKTextAlign.Left,
        FakeBoldText = false
    )

let chrSizeInfo =
    let skp = getPaint ()

    let mutable rect = SKRect()
    let width = skp.MeasureText("\u5BBD", &rect)

    {| SingleWidth = width / 2.0f
       Height = rect.Height |}

/// 额外预留宽度，保持文字不贴边
let extraWidth = 10.0f

/// Skia Canvas缩放比率
let drawScale = 1.5f

let rowSpacing =
    let space = chrSizeInfo.Height * 0.15f |> ceil

    space + 0.5f |> round // 保证能被2整除

let measureText (str: string, skp: SKPaint) =
    if String.IsNullOrEmpty(str) then
        SKRect.Empty
    else
        let mutable rect = SKRect()
        let width = skp.MeasureText(str, &rect)

        // 警告：MeasureText中rect不考虑空白字符
        // 如果有任何字符，保证高度为一个汉字高度
        SKRect.Create(rect.Left, rect.Top, width, max chrSizeInfo.Height rect.Height)
