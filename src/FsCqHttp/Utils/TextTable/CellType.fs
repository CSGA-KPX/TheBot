namespace KPX.FsCqHttp.Utils.TextTable

open System

open KPX.FsCqHttp.Utils.TextResponse


[<RequireQualifiedAccess>]
[<Struct>]
type CellAlign =
    | Left
    | Right

[<Struct>]
type TableCell =
    { Text : string
      Align : CellAlign }

    member x.IsLeftAlign = x.Align = CellAlign.Left
    member x.IsRightAlign = x.Align = CellAlign.Right

    member internal x.DisplayWidthOf(measurer : ImageMeasurer) = measurer.MeasureWidthByConfig(x.Text)

    static member private GetDefaultAlign(o : obj) =
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

    /// 根据对象类型，创建适合的单元格
    static member CreateFrom(o : obj, ?align : CellAlign) =
        if o :? TableCell then
            o :?> TableCell
        else
            { Text = o.ToString()
              Align = defaultArg align (TableCell.GetDefaultAlign(o)) }
