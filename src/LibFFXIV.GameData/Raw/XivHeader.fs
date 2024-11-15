namespace LibFFXIV.GameData.Raw

open System
open System.Collections.Generic
open System.Text.RegularExpressions


[<RequireQualifiedAccess>]
type XivCellType =
    | String
    | Number
    | Bool
    | Unknown
    | JsonUnknown

    override x.ToString() =
        match x with
        | String -> "str"
        | Number -> "int32"
        | Bool -> "bool"
        | Unknown -> XivCellType.JsonImportUnknownType
        | JsonUnknown -> XivCellType.JsonImportUnknownType

    static member val JsonImportUnknownType = "UNKNOWN-JSON"

    static member FromString(str) =
        let ret = 
            match str with
            | "bit"
            | "bool" -> XivCellType.Bool
            | "int64"
            | "uint64"
            | "int32"
            | "uint32"
            | "int16"
            | "uint16"
            | "byte"
            | "sbyte" -> XivCellType.Number
            | "str"
            | "string" -> XivCellType.String
            | _ ->
                if str = XivCellType.JsonImportUnknownType then
                    XivCellType.JsonUnknown
                else
                    XivCellType.Unknown

        //printfn $"converted {str} to {ret}"

        ret

    static member val NumberRegex = Regex("^[0-9, ]+$", RegexOptions.Compiled)
    static member val BoolRegex = Regex("^True|False$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

    static member GuessType (values : string []) =
        if values |> Array.forall (fun x -> XivCellType.NumberRegex.IsMatch(x)) then
            XivCellType.Number
        elif values |> Array.forall (fun x -> XivCellType.BoolRegex.IsMatch(x)) then
            XivCellType.Bool
        else
            XivCellType.String

type XivHeaderItem =
    {
        /// Raw column name
        ///
        /// key, 0, 1, 2, 3 ...
        OrignalKeyName: string
        /// Suggested column name
        ///
        /// as the second row in csv.
        ColumnName: string
        /// Suggested column type name
        ///
        /// as the third row in csv.
        TypeName: string
    }

[<Struct>]
type XivHeaderIndex =
    /// Raw column index
    ///
    /// Same as OrignalKeyName except 'key'
    | RawIndex of raw: int
    /// Index associated with XivHeader class
    | HeaderIndex of hdr: int

    /// <summary>
    /// Convert XivHeaderIndex to raw index.
    /// </summary>
    member x.ToRawIndex =
        match x with
        | RawIndex idx -> idx
        | HeaderIndex idx -> idx - 1

    /// <summary>
    /// Convert XivHeaderIndex to int index.
    /// </summary>
    member x.ToHdrIndex =
        match x with
        | RawIndex idx -> idx + 1
        | HeaderIndex idx -> idx


[<Sealed>]
type XivHeader(items: XivHeaderItem[]) =
    // #,Name,Name,Name,Name,Name
    let nameToId =
        [| for i = 0 to items.Length - 1 do
               let item = items.[i]

               if not <| String.IsNullOrEmpty(item.ColumnName) then
                   yield (item.ColumnName, i) |]
        |> readOnlyDict

    // int32,str,str,str,str,str
    let idToType =
        [| for i = 0 to items.Length - 1 do
               let item = items.[i]

               if item.TypeName.StartsWith("bit") then
                   // remove bit&01 bit&02
                   yield "bit"
               else
                   yield item.TypeName |]


    /// <summary>
    /// Get header index of given column name.
    ///
    /// This index contains the \# column.
    ///
    /// \#->0, 0->1, 1->2...
    /// </summary>
    /// <param name="col">Column name</param>
    member internal x.GetIndex(col) =
        try
            HeaderIndex(nameToId.[col])
        with :? KeyNotFoundException ->
            printfn $"Unknown column name : %s{col}"
            printfn "Known names are：%s" (String.Join(" ", nameToId.Keys))
            reraise ()

    /// <summary>
    /// Get type name of given header index.
    /// </summary>
    /// <param name="idx">Header index</param>
    member x.GetFieldType(idx: XivHeaderIndex) =
        let t = idToType.[idx.ToHdrIndex]

        if t.ToLowerInvariant() = "row" then
            items.[idx.ToHdrIndex].ColumnName
        else
            t

    /// <summary>
    /// Get type name of given header index.
    /// </summary>
    /// <param name="idx">Header index</param>
    member x.GetTypedFieldType(idx: XivHeaderIndex) =
        XivCellType.FromString(x.GetFieldType(idx))

    /// <summary>
    /// Get type name of given column name.
    /// </summary>
    /// <param name="col"></param>
    member x.GetFieldType(col) = idToType.[x.GetIndex(col).ToHdrIndex]

    /// <summary>
    /// Get header items.
    /// </summary>
    member x.Headers = items :> IReadOnlyList<_>
