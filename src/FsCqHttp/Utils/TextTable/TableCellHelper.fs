namespace KPX.FsCqHttp.Utils.TextResponse

open System
open System.Numerics


type TableCellHelper =
    static member RoundSigDigits<'Number when INumber<'Number> and 'Number: equality>(value: 'Number, sigDigits: int) =
        let value = Convert.ToDouble(value)

        if value = 0.0 then
            0.0
        elif sigDigits = 0 then
            value
        else
            let scale = 10.0 ** ((value |> abs |> log10 |> floor) + 1.0)

            scale * Math.Round(value / scale, sigDigits)

    static member FormatTimeSpan(ts: TimeSpan) =
        if ts = TimeSpan.MaxValue then "--"
        elif ts.TotalDays >= 1.0 then $"%.0f{ts.TotalDays}天前"
        elif ts.TotalHours >= 1.0 then $"%.0f{ts.TotalHours}时前"
        else $"%.0f{ts.TotalMinutes}分前"

    static member HumanReadble<'Number when INumber<'Number>>(value: 'Number, ignoreDigits: bool) =
        let sigDigits = 4

        let mutable value = TableCellHelper.RoundSigDigits(Convert.ToDouble(value), sigDigits)

        let str =
            match value with
            | 0.0 -> "0"
            | _ when Double.IsNaN(value) -> "NaN"
            | _ when Double.IsNegativeInfinity(value) -> "+inf%"
            | _ when Double.IsPositiveInfinity(value) -> "-inf%"
            | _ ->
                let pow10 = ((value |> abs |> log10 |> floor) + 1.0) |> floor |> int

                let scale, postfix = if pow10 >= 9 then 8.0, "亿" else 0.0, ""

                let hasEnoughDigits = (pow10 - (int scale) + 1) >= sigDigits

                if hasEnoughDigits || ignoreDigits then
                    String.Format("{0:N0}{1}", value / 10.0 ** scale, postfix)
                else
                    String.Format("{0:N2}{1}", value / 10.0 ** scale, postfix)

        TableCell(str, Align = TextAlignment.Right)
