namespace KPX.FsCqHttp.Utils.TextResponse

open System
open System.Text
open SkiaSharp


// 考虑换成struct
type CellBuilder() =

    member val Builder = StringBuilder()
    member val Content = ResizeArray<string>()
    member val Align = TextAlignment.Left with get, set
    member val FakeBold = false with get, set
    member val TextColor = SKColors.Black with get, set

    [<CustomOperation("integer")>]
    member _.Integer(x: CellBuilder, value: obj) =
        TableCellHelper.EnsureNumberType value
        x.Align <- TextAlignment.Right
        x.Builder.AppendFormat("{0:N0}", value) |> ignore
        x

    [<CustomOperation("integerSig4")>]
    member _.IntegerSig4(x: CellBuilder, value: obj) =
        TableCellHelper.EnsureNumberType value
        x.Align <- TextAlignment.Right

        x.Builder.AppendFormat("{0:N0}", TableCellHelper.RoundSigDigits(value, 4))
        |> ignore

        x

    [<CustomOperation("quantity")>]
    /// <summary>
    /// 整数无小数点，小数保留2位
    /// </summary>
    member _.Quantity(x: CellBuilder, value: obj) =
        TableCellHelper.EnsureNumberType value
        x.Align <- TextAlignment.Right

        let str = String.Format("{0:N2}", value)

        if str.EndsWith(".00") then
            x.Builder.AppendFormat("{0:N0}", value) |> ignore
        else
            x.Builder.Append(str) |> ignore

        x

    [<CustomOperation("float")>]
    member _.Float(x: CellBuilder, value: obj) =
        TableCellHelper.EnsureNumberType value
        x.Align <- TextAlignment.Right
        x.Builder.AppendFormat("{0:N2}", value) |> ignore
        x

    [<CustomOperation("floatSig4")>]
    member _.FloatSig4(x: CellBuilder, value: obj) =
        TableCellHelper.EnsureNumberType value
        x.Align <- TextAlignment.Right

        x.Builder.AppendFormat("{0:N2}", TableCellHelper.RoundSigDigits(value, 4))
        |> ignore

        x

    [<CustomOperation("number")>]
    member _.Number(x: CellBuilder, value: obj) =
        TableCellHelper.EnsureNumberType value
        x.Align <- TextAlignment.Right

        let sigDigits = 4

        let mutable value = TableCellHelper.RoundSigDigits(Convert.ToDouble(value), sigDigits)

        let cellString =
            match value with
            | 0.0 -> "0"
            | _ when Double.IsNaN(value) -> "NaN"
            | _ when Double.IsNegativeInfinity(value) -> "+inf%"
            | _ when Double.IsPositiveInfinity(value) -> "-inf%"
            | _ ->
                let pow10 = ((value |> abs |> log10 |> floor) + 1.0) |> floor |> int

                let scale, postfix = if pow10 >= 9 then 8.0, "亿" else 0.0, ""

                let hasEnoughDigits = (pow10 - (int scale) + 1) >= sigDigits

                if hasEnoughDigits then
                    String.Format("{0:N0}{1}", value / 10.0 ** scale, postfix)
                else
                    String.Format("{0:N2}{1}", value / 10.0 ** scale, postfix)

        x.Builder.Append(cellString) |> ignore
        x

    [<CustomOperation("percent")>]
    member _.Percent(x: CellBuilder, value: float) =
        TableCellHelper.EnsureNumberType value
        x.Align <- TextAlignment.Right
        x.Builder.AppendFormat("{0:P2}", value) |> ignore
        x

    [<CustomOperation("toTimeSpan")>]
    member _.ToTimeSpan(x: CellBuilder, value: DateTimeOffset) =
        x.Align <- TextAlignment.Right

        TableCellHelper.FormatTimeSpan(DateTimeOffset.Now - value)
        |> x.Builder.Append
        |> ignore

        x

    [<CustomOperation("timeSpan")>]
    member _.TimeSpan(x: CellBuilder, value: TimeSpan) =
        x.Align <- TextAlignment.Right

        TableCellHelper.FormatTimeSpan value |> x.Builder.Append |> ignore

        x

    [<CustomOperation("dateTime")>]
    member _.DateTime(x: CellBuilder, value: DateTimeOffset) =
        x.Align <- TextAlignment.Right

        value.ToLocalTime().ToString("yyyy/MM/dd HH:mm") |> x.Builder.Append |> ignore

        x

    [<CustomOperation("endLine")>]
    member _.EndLine(x: CellBuilder) =
        x.Content.Add(x.Builder.ToString())
        x.Builder.Clear() |> ignore

        x

    [<CustomOperation("newLine")>]
    member _.NewLine(x: CellBuilder) =
        if x.Builder.Length <> 0 then
            x.Content.Add(x.Builder.ToString())
            x.Builder.Clear() |> ignore

        x.Content.Add(String.Empty)

        x

    member x.ToTableCells(dParams : DrawParameters) =
        if x.Builder.Length <> 0 then
            x.EndLine(x) |> ignore
        // 保证至少有一个？
        if x.Content.Count = 0 then
            x.Content.Add(String.Empty)

        let contains = x.Content |> Seq.exists (fun str -> str.Contains('\n'))

        if contains then
            invalidOp "TableCell不允许包含多行文本，请使用相关指令拆分"

        [| for line in x.Content do
               let cell = TableCell(line, dParams)
               cell.Align <- x.Align
               cell.FakeBold <- x.FakeBold
               cell.TextColor <- x.TextColor

               cell |]

    [<CustomOperation("literal")>]
    member _.Literal(x: CellBuilder, item: obj) =
        x.Align <- TextAlignment.Left
        x.Builder.Append(item) |> ignore
        x

    [<CustomOperation("leftLiteral")>]
    member _.LeftLiteral(x: CellBuilder, item: obj) =
        x.Align <- TextAlignment.Left
        x.Builder.Append(item) |> ignore
        x

    [<CustomOperation("rightLiteral")>]
    member _.RightLiteral(x: CellBuilder, item: obj) =
        x.Align <- TextAlignment.Right
        x.Builder.Append(item) |> ignore
        x

    [<CustomOperation("leftPad")>]
    member _.LeftPad(x: CellBuilder) =
        x.Align <- TextAlignment.Left
        x.Builder.Append("") |> ignore
        x

    [<CustomOperation("rightPad")>]
    member _.RightPad(x: CellBuilder) =
        x.Align <- TextAlignment.Right
        x.Builder.Append("") |> ignore
        x

    [<CustomOperation("leftAlign")>]
    member _.LeftAlign(x: CellBuilder) =
        x.Align <- TextAlignment.Left
        x

    [<CustomOperation("rightAlign")>]
    member _.RightAlign(x: CellBuilder) =
        x.Align <- TextAlignment.Right
        x

    [<CustomOperation("setBold")>]
    member _.SetBold(x: CellBuilder) =
        x.FakeBold <- true
        x

    [<CustomOperation("unsetBold")>]
    member _.UnsetBold(x: CellBuilder) =
        x.FakeBold <- false
        x

    [<CustomOperation("splitString")>]
    member _.SplitLines(x: CellBuilder, str: String) =
        if x.Builder.Length <> 0 then
            x.NewLine(x) |> ignore

        x.Content.AddRange(str.Split([| "\r\n"; "\r"; "\n" |], StringSplitOptions.None))
        x

    [<CustomOperation("addLines")>]
    member _.AddLines(x: CellBuilder, lines: String []) =
        if x.Builder.Length <> 0 then
            x.NewLine(x) |> ignore

        x.Content.AddRange(lines)
        x

    member x.Yield _ = x

    member x.Zero() = x

    [<CustomOperation("setTextWhite")>]
    member _.SetTextWhite(x: CellBuilder) =
        x.TextColor <- SKColors.White
        x

    [<CustomOperation("setTextSilver")>]
    member _.SetTextSilver(x: CellBuilder) =
        x.TextColor <- SKColors.Silver
        x

    [<CustomOperation("setTextGray")>]
    member _.SetTextGray(x: CellBuilder) =
        x.TextColor <- SKColors.Gray
        x

    [<CustomOperation("setTextBlack")>]
    member _.SetTextBlack(x: CellBuilder) =
        x.TextColor <- SKColors.Black
        x

    [<CustomOperation("setTextRed")>]
    member _.SetTextRed(x: CellBuilder) =
        x.TextColor <- SKColors.Red
        x

    [<CustomOperation("setTextMaroon")>]
    member _.SetTextMaroon(x: CellBuilder) =
        x.TextColor <- SKColors.Maroon
        x

    [<CustomOperation("setTextYellow")>]
    member _.SetTextYellow(x: CellBuilder) =
        x.TextColor <- SKColors.Yellow
        x

    [<CustomOperation("setTextOlive")>]
    member _.SetTextOlive(x: CellBuilder) =
        x.TextColor <- SKColors.Olive
        x

    [<CustomOperation("setTextLime")>]
    member _.SetTextLime(x: CellBuilder) =
        x.TextColor <- SKColors.Lime
        x

    [<CustomOperation("setTextGreen")>]
    member _.SetTextGreen(x: CellBuilder) =
        x.TextColor <- SKColors.Green
        x

    [<CustomOperation("setTextAqua")>]
    member _.SetTextAqua(x: CellBuilder) =
        x.TextColor <- SKColors.Aqua
        x

    [<CustomOperation("setTextTeal")>]
    member _.SetTextTeal(x: CellBuilder) =
        x.TextColor <- SKColors.Teal
        x

    [<CustomOperation("setTextBlue")>]
    member _.SetTextBlue(x: CellBuilder) =
        x.TextColor <- SKColors.Blue
        x

    [<CustomOperation("setTextNavy")>]
    member _.SetTextNavy(x: CellBuilder) =
        x.TextColor <- SKColors.Navy
        x

    [<CustomOperation("setTextFuchsia")>]
    member _.SetTextFuchsia(x: CellBuilder) =
        x.TextColor <- SKColors.Fuchsia
        x

    [<CustomOperation("setTextPurple")>]
    member _.SetTextPurple(x: CellBuilder) =
        x.TextColor <- SKColors.Purple
        x
