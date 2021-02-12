namespace KPX.FsCqHttp.Utils.TextTable

open System


[<RequireQualifiedAccess>]
[<Struct>]
type CellAlign =
    | Left
    | Right

type TableCell(text : string) =
    member val Text = text with get, set
    member val Align = CellAlign.Left with get, set

    member x.IsLeftAlign = x.Align = CellAlign.Left
    member x.IsRightAlign = x.Align = CellAlign.Right

    member x.DisplayWidth =
        KPX.FsCqHttp.Config.Output.TextTable.StrDispLen(x.Text)

    static member private GetDefaultAlign(o : obj) =
        match o with
        | :? TimeSpan -> CellAlign.Right
        | _ ->
            // 检查TypeCode比类型检查要快
            match Type.GetTypeCode(o.GetType()) with
            | TypeCode.Byte
            | TypeCode.SByte
            | TypeCode.UInt16
            | TypeCode.UInt32
            | TypeCode.UInt64
            | TypeCode.Int16
            | TypeCode.Int32
            | TypeCode.Int64
            | TypeCode.Decimal
            | TypeCode.Double
            | TypeCode.Single
            | TypeCode.DateTime -> CellAlign.Right
            | _ -> CellAlign.Left

    static member private ConvertToString(o : obj) =
        match o with
        | :? string as str -> str
        | :? int32 as i -> String.Format("{0:N0}", i)
        | :? uint32 as i -> String.Format("{0:N0}", i)
        | :? float as f -> String.Format("{0:N2}", f)
        | :? TimeSpan as ts ->
            if ts.TotalDays >= 1.0 then sprintf "%.0f天前" ts.TotalDays
            elif ts.TotalHours >= 1.0 then sprintf "%.0f时前" ts.TotalHours
            else sprintf "%.0f分前" ts.TotalMinutes
        | :? DateTimeOffset as dto -> TableCell.ConvertToString(DateTimeOffset.Now - dto)
        | :? DateTime as dt -> TableCell.ConvertToString(DateTime.Now - dt)
        | _ -> o.ToString()

    /// 根据对象类型，创建适合的单元格
    static member CreateFrom(o : obj, ?align : CellAlign) =
        if o :? TableCell then
            o :?> TableCell
        else
            let cell = TableCell(TableCell.ConvertToString(o))
            cell.Align <- defaultArg align (TableCell.GetDefaultAlign(o))
            cell
