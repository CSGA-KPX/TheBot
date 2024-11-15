namespace LibFFXIV.GameData.Provider


open System

open LibFFXIV.GameData.Raw

[<Sealed>]
type TypedCell(row: XivRow, hdrIdx: int) =

    new(row: XivRow, fieldName: string) =
        let idx = row.Sheet.Header.GetIndex(fieldName)
        TypedCell(row, idx.ToHdrIndex)

    /// Get the untyped XivRow
    member x.Row = row

    /// XivHeaderIndex of this cell
    member val Index = XivHeaderIndex.HeaderIndex hdrIdx

    member x.Type = row.Sheet.Header.GetTypedFieldType(x.Index)

    member x.RawType = row.Sheet.Header.GetFieldType(x.Index)

    member x.AsInt() = row.As<int>(x.Index)
    member x.AsUInt() = row.As<uint>(x.Index)

    member x.AsInt16() = row.As<int16>(x.Index)
    member x.AsInt32() = row.As<int32>(x.Index)
    member x.AsInt64() = row.As<int64>(x.Index)

    member x.AsUInt16() = row.As<uint16>(x.Index)
    member x.AsUInt32() = row.As<uint32>(x.Index)
    member x.AsUInt64() = row.As<uint64>(x.Index)

    member x.AsDouble() = row.As<float>(x.Index)
    member x.AsSingle() = row.As<float32>(x.Index)

    member x.AsBool() = row.As<bool>(x.Index)

    member x.AsString() = row.As<string>(x.Index)
