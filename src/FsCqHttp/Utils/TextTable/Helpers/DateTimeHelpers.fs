namespace KPX.FsCqHttp.Utils.TextTable

open System

[<Sealed>]
[<AutoOpen>]
type DateTimeHelpers =
    static member HumanTimeSpan(ts : TimeSpan) =
        if ts = TimeSpan.MaxValue then "--"
        elif ts.TotalDays >= 1.0 then $"%.0f{ts.TotalDays}天前"
        elif ts.TotalHours >= 1.0 then $"%.0f{ts.TotalHours}时前"
        else $"%.0f{ts.TotalMinutes}分前"
        |> RightAlignCell

    static member HumanTimeSpan(dt : DateTime) =
        DateTimeHelpers.HumanTimeSpan(DateTime.Now - dt)

    static member HumanTimeSpan(dto : DateTimeOffset) =
        DateTimeHelpers.HumanTimeSpan(DateTimeOffset.Now - dto)

    static member ToDate(dt : DateTime) = 
        dt.ToString("yyyy/MM/dd HH:mm")
        |> RightAlignCell

    static member ToDate(dto : DateTimeOffset) = 
        dto.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
        |> RightAlignCell
