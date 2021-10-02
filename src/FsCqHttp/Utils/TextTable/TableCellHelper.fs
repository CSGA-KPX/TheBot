namespace KPX.FsCqHttp.Utils.TextResponse

open System


type internal TableCellHelper private () =
    static let numberTypes =
        seq {
            typeof<sbyte>
            typeof<byte>
            typeof<int16>
            typeof<uint16>
            typeof<int32>
            typeof<uint32>
            typeof<int64>
            typeof<uint64>
            typeof<bigint>
            typeof<float32>
            typeof<float>
            typeof<decimal>
        }
        |> Seq.map (fun t -> t.FullName)
        |> set

    static member EnsureNumberType(item : obj) =
        let name = item.GetType().FullName

        if not <| numberTypes.Contains(name) then
            raise
            <| InvalidOperationException($"需要数值类型，而给定的是%s{name}")

    static member RoundSigDigits(number : obj, sigDigits : int) =
        let value = Convert.ToDouble(number)

        if value = 0.0 then
            0.0
        elif sigDigits = 0 then
            value
        else
            let scale =
                10.0 ** ((value |> abs |> log10 |> floor) + 1.0)

            scale * Math.Round(value / scale, sigDigits)

    static member FormatTimeSpan(ts : TimeSpan) =
        if ts = TimeSpan.MaxValue then "--"
        elif ts.TotalDays >= 1.0 then $"%.0f{ts.TotalDays}天前"
        elif ts.TotalHours >= 1.0 then $"%.0f{ts.TotalHours}时前"
        else $"%.0f{ts.TotalMinutes}分前"
