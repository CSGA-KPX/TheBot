namespace KPX.FsCqHttp.Utils.TextTable

open System

[<Sealed>]
[<AutoOpen>]
type DateTimeHelpers =
    static member HumanTimeSpan(ts : TimeSpan) =
        if ts.TotalDays >= 1.0 then sprintf "%.0f天前" ts.TotalDays
        elif ts.TotalHours >= 1.0 then sprintf "%.0f时前" ts.TotalHours
        else sprintf "%.0f分前" ts.TotalMinutes
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
