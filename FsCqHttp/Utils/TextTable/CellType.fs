namespace KPX.FsCqHttp.Utils.TextTable

open System


/// 用于区分显示细节
[<DefaultAugmentation(false)>]
type TableCell =
    /// 左对齐单元格，用于文本类型
    | LeftAlignCell of string
    /// 右对齐单元格，用于数字类型
    | RightAlignCell of string

    member x.IsRightAlign =
        match x with
        | RightAlignCell _ -> true
        | _ -> false

    member x.IsLeftAlign =
        match x with
        | RightAlignCell _ -> false
        | _ -> true

    /// 获取单元格的原始内容
    member x.Value =
        match x with
        | RightAlignCell v -> v
        | LeftAlignCell v -> v

    /// 文本显示长度
    member x.DisplayWidth =
        KPX.FsCqHttp.Config.Output.TextTable.StrDispLen(x.ToString())

    static member private IsObjectRightAlign(o : obj) =
        match o with
        | :? TimeSpan -> true
        | _ ->
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
            | TypeCode.DateTime -> true
            | _ -> false

    static member private ConvertToString(o : obj) =
        match o with
        | :? string as str -> str
        | :? int32 as i -> System.String.Format("{0:N0}", i)
        | :? uint32 as i -> System.String.Format("{0:N0}", i)
        | :? float as f ->
            let fmt =
                if (f % 1.0) <> 0.0 then "{0:N2}" else "{0:N0}"

            System.String.Format(fmt, f)
        | :? TimeSpan as ts ->
            if ts.TotalDays >= 1.0 then sprintf "%.0f天前" ts.TotalDays
            elif ts.TotalHours >= 1.0 then sprintf "%.0f时前" ts.TotalHours
            elif ts.TotalMinutes >= 1.0 then sprintf "%.0f分前" ts.TotalMinutes
            else "刚刚"
        | :? DateTimeOffset as dto -> TableCell.ConvertToString(DateTimeOffset.Now - dto)
        | :? DateTime as dt -> TableCell.ConvertToString(DateTime.Now - dt)
        | _ -> o.ToString()

    /// 根据对象类型，创建适合的单元格
    static member CreateFrom(o : obj) =
        if o :? TableCell then
            o :?> TableCell
        else
            let str = TableCell.ConvertToString(o)
            if TableCell.IsObjectRightAlign(o) then RightAlignCell str else LeftAlignCell str

    /// 强制为左对齐
    static member CreateLeftAlign(o : obj) =
        match TableCell.CreateFrom(o) with
        | RightAlignCell x -> LeftAlignCell x
        | x -> x

    /// 强制为右对齐
    static member CreateRightAlign(o : obj) =
        match TableCell.CreateFrom(o) with
        | LeftAlignCell x -> RightAlignCell x
        | x -> x
