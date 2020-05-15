module XivData.Utils

open System
open System.Reflection
open LibFFXIV.GameData.Raw

let TryGetToOption(x : bool, y : 'Value) =
    if x then Some(y)
    else None

let FsMapper = LiteDB.FSharp.FSharpBsonMapper()

let Db =
    let dbFile = @"Filename=xivdata.db; Journal=true; Flush=true; Cache Size=500"
    let db = new LiteDB.LiteDatabase(dbFile, FsMapper)
    db

let ClearDb() =
    for name in Db.GetCollectionNames() |> Seq.toArray do
        Db.DropCollection(name) |> ignore
    Db.Shrink() |> ignore

let GlobalVerCollection = 
    let ss = 
        let archive = 
                let ResName = "BotData.ffxiv-datamining-master.zip"
                let assembly = Reflection.Assembly.GetExecutingAssembly()
                let stream = assembly.GetManifestResourceStream(ResName)
                new IO.Compression.ZipArchive(stream, IO.Compression.ZipArchiveMode.Read)
        EmbeddedCsvStroage(archive, "ffxiv-datamining-master/csv/") :> ISheetStroage<seq<string []>>
    EmbeddedXivCollection(ss, XivLanguage.None, true) :> IXivCollection

/// 整合两个不同版本的表
//
/// b >= a
//
/// func a b -> bool = true then b else a
let MergeSheet(a : IXivSheet, b : IXivSheet, func : XivRow * XivRow -> bool ) = 
    if a.Name <> b.Name then
        invalidOp "Must merge on same sheet!"

    seq {
        for row in b do
            if a.ContainsKey(row.Key) then
                let rowA = a.[row.Key.Main, row.Key.Alt]
                let ret = func(rowA, row)
                if ret then
                    yield row
                else
                    yield rowA
            else
                yield row
    }

type IXivDataSource =
    abstract BuildOrder : int

[<AbstractClass>]
type XivDataSource<'Id, 'Value>() as x =
    let cName = x.GetType().Name
    let col = Db.GetCollection<'Value>(cName)

    member internal x.Collection = col

    member x.Count() = col.Count()

    member internal x.Item(id : 'Id) = x.TryLookupById(id)

    member _.ClearCollection() = Db.DropCollection(cName) |> ignore
    member _.CollectionExists() = Db.CollectionExists(cName)

    /// 部分类型Id无效，请用别的方法
    /// 有可能返回null
    member internal _.LookupById(id : 'Id) = col.FindById(LiteDB.BsonValue(id))


    /// 部分类型Id无效，请用别的方法
    member internal _.TryLookupById(id : 'Id) =
        let ret = col.FindById(LiteDB.BsonValue(id))
        if isNull (box ret) then None
        else Some ret

    abstract BuildCollection : unit -> unit

    interface Collections.IEnumerable with
        member x.GetEnumerator() = col.FindAll().GetEnumerator() :> Collections.IEnumerator


    interface Collections.Generic.IEnumerable<'Value> with
        member x.GetEnumerator() = col.FindAll().GetEnumerator()

    interface IXivDataSource with
        override x.BuildOrder = 0

let InitAllDb() =
    Assembly.GetExecutingAssembly().GetTypes()
    |> Array.filter (fun t -> (typeof<IXivDataSource>.IsAssignableFrom(t) && (not (t.IsAbstract))))
    |> Array.sortBy (fun t -> 
        let p = t.GetProperty("Instance", BindingFlags.Public ||| BindingFlags.Static)
        let o = p.GetValue(null) :?> IXivDataSource
        printfn ">>> Found %s : %i" t.Name o.BuildOrder
        o.BuildOrder
    )
    |> Array.iter (fun t ->
        printfn ">>> Building %s" t.Name
        let p = t.GetProperty("Instance", BindingFlags.Public ||| BindingFlags.Static)
        let o = p.GetValue(null)
        t.GetMethod("BuildCollection").Invoke(o, null) |> ignore)
