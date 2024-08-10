namespace rec LibFFXIV.GameData.Raw

open System
open System.Collections.Generic

open LibFFXIV.GameData


/// <summary>
/// Holds one row of the a sheet
/// </summary>
/// <param name="sheet">The parent XivSheet</param>
/// <param name="data">Row data</param>
type XivRow(sheet: XivSheet, data: string []) =

    /// Get parent XivSheet
    member x.Sheet = sheet

    /// Primary key of this row
    member val Key = XivKey.FromString(data.[0])

    member x.RawData = data :> IReadOnlyList<_>

    /// <summary>
    /// Get data of given index.
    /// </summary>
    /// <typeparam name="'T">Convert string value to</typeparam>
    member x.As<'T when 'T :> IConvertible>(idx: XivHeaderIndex) =
        let t = sheet.Header.GetFieldType(idx)
        let id = idx.ToHdrIndex

        if t = "int64" then
            let str = data.[id]

            let chunk =
                str.Split([| ','; ' ' |], StringSplitOptions.RemoveEmptyEntries)
                |> Array.map int64

            let i64 = chunk.[0] + (chunk.[1] <<< 16) + (chunk.[2] <<< 32) + (chunk.[3] <<< 48)

            Convert.ChangeType(i64, typeof<'T>) :?> 'T
        else
            Convert.ChangeType(data.[id], typeof<'T>) :?> 'T

    /// <summary>
    /// Get data of given column name.
    /// </summary>
    /// <typeparam name="'T">Convert string value to</typeparam>
    member x.As<'T when 'T :> IConvertible>(name: string) = x.As<'T>(sheet.Header.GetIndex(name))

    /// <summary>
    /// Get array data of given column, in 'prefix[0 .. len]'
    /// </summary>
    /// <typeparam name="'T">Convert string value to</typeparam>
    member x.AsArray<'T when 'T :> IConvertible>(prefix, len) =
        [| for i = 0 to len - 1 do
               let key = $"%s{prefix}[%i{i}]"
               yield (x.As<'T>(key)) |]

    /// Convert index to reference object.
    member internal x.AsRowRef(idx: XivHeaderIndex) =
        let id = idx.ToHdrIndex
        let str = data.[id]
        let t = sheet.Header.GetFieldType(idx)

        if sheet.Collection.SheetExists(t) then
            { Sheet = t; Key = str |> int32 }
        else
            failwithf $"Sheet not found in collection: %s{t}"

    /// Convert column name to reference object.
    member internal x.AsRowRef(name: string) = x.AsRowRef(sheet.Header.GetIndex(name))

    /// Lookup row in target sheet
    member x.AsRow(idx: XivHeaderIndex) =
        let r = x.AsRowRef(idx)

        sheet.Collection.GetSheet(r.Sheet).[r.Key]

    /// Lookup row in target sheet
    member x.AsRow(str: string) = x.AsRow(sheet.Header.GetIndex(str))

    /// Lookup rows in target sheet, in 'prefix[0 .. len]'
    member x.AsRowArray(prefix, len) =
        [| for i = 0 to len - 1 do
               let key = $"%s{prefix}[%i{i}]"
               yield (x.AsRowRef(key)) |]
        |> Array.map (fun r -> sheet.Collection.GetSheet(r.Sheet).[r.Key])

/// <summary>
/// Basic Sheet Implementation.
/// </summary>
/// <param name="name">Sheet name</param>
/// <param name="col">Parent XivCollection</param>
/// <param name="hdr">XivHeader of the sheet</param>
type XivSheetBase(name, col: XivCollection, hdr) =
    let rowCache = Dictionary<XivKey, XivRow>()
    let mutable cacheInitialized = false
    let mutable rowSourceSet = false
    let mutable rowSeq: seq<XivRow> = Seq.empty

    let metaInfo = Dictionary<string, obj>()

    /// Stores runtime information.
    ///
    /// E.g. cache generated array-column names.
    member internal x.MetaInfo = metaInfo :> IDictionary<_, _>

    /// Cache sheet rows to support random access.
    member x.EnsureCached() =
        if not cacheInitialized then
            if not rowSourceSet then
                invalidOp "Call SetRowSource first."

            for row in rowSeq do
                rowCache.Add(row.Key, row)

            cacheInitialized <- true
            rowSeq <- Seq.empty

    /// Set row data source.
    ///
    /// XivRow requires XivSheet, So row source needs to be set later.
    member internal x.SetRowSource(seq) =
        rowSourceSet <- true
        rowSeq <- seq

    member internal x.Rows =
        if cacheInitialized then
            (rowCache.Values |> Seq.map (fun x -> x))
                .GetEnumerator()
        else
            if not rowSourceSet then
                invalidOp "Call SetRowSource first."

            rowSeq.GetEnumerator()

    member x.Name: string = name

    member x.Collection: XivCollection = col

    member x.Header: XivHeader = hdr

    member internal x.GetItem(key: XivKey) =
        x.EnsureCached()

        if rowCache.ContainsKey(key) then
            rowCache.[key]
        else
            raise <| KeyNotFoundException $"Cannot found key %A{key} in sheet %s{name}"

    /// Check is key exists. Call to this method will cache the sheet.
    member x.ContainsKey(key: XivKey) =
        x.EnsureCached()
        rowCache.ContainsKey(key)

    member x.ContainsKey(main: int) =
        x.EnsureCached()
        rowCache.ContainsKey({ Main = main; Alt = 0 })

type XivSheet(name, col: XivCollection, hdr) =
    inherit XivSheetBase(name, col, hdr)

    member x.Item(key: XivKey) = x.GetItem(key)

    member x.Item(main: int) = x.GetItem({ Main = main; Alt = 0 })

    interface IEnumerable<XivRow> with
        member x.GetEnumerator() = x.Rows

    interface Collections.IEnumerable with
        member x.GetEnumerator() = x.Rows :> Collections.IEnumerator

[<AbstractClass>]
type XivCollection(lang) =
    // use weak reference to allow cached sheet to be GCed.
    let weakCache = Dictionary<string, WeakReference<XivSheet>>()

    member x.Language: XivLanguage = lang

    abstract GetAllSheetNames: unit -> seq<string>

    abstract SheetExists: string -> bool

    /// Create sheet.
    abstract GetSheetUncached: name: string -> XivSheet

    /// Create or get cached sheet.
    member x.GetSheet(name) : XivSheet =

        if weakCache.ContainsKey(name) then
            let succ, sht = weakCache.[name].TryGetTarget()

            if succ then
                sht
            else
                let sht = x.GetSheetUncached(name)
                weakCache.[name].SetTarget(sht)
                sht
        else
            let sht = x.GetSheetUncached(name)
            weakCache.[name] <- WeakReference<_>(sht)
            sht

    /// Release associated resource.
    /// Call base.Dispose() to dispose weak cache.
    abstract Dispose: unit -> unit

    default x.Dispose() = weakCache.Clear()

    interface IDisposable with
        member x.Dispose() = x.Dispose()
