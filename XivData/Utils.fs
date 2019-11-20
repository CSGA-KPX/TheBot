module XivData.Utils
open System
open System.Reflection

let TryGetToOption (x : bool, y: 'Value) = 
    if x then
        Some(y)
    else
        None

let FsMapper = new LiteDB.FSharp.FSharpBsonMapper()

let Db = 
    let dbFile = @"Filename=xivdata.db; Journal=true; Flush=true; Cache Size=500"
    let db = new LiteDB.LiteDatabase(dbFile, FsMapper)
    db

let ClearDb() = 
    for name in Db.GetCollectionNames() |> Seq.toArray do 
        Db.DropCollection(name) |> ignore
    Db.Shrink() |> ignore

type IXivDataSource = 
    interface end

[<AbstractClass>]
type XivDataSource<'Id, 'Value>() as x = 
    let cName = x.GetType().Name
    let col   = Db.GetCollection<'Value>(cName)

    member internal x.Collection = col

    member x.Count() = col.Count()

    member x.Item (id : 'Id) = x.TryLookupById(id)

    member _.ClearCollection () = Db.DropCollection(cName) |> ignore
    member _.CollectionExists() = Db.CollectionExists(cName)

    /// 部分类型Id无效，请用别的方法
    /// 有可能返回null
    member _.LookupById(id : 'Id) =
        col.FindById(new LiteDB.BsonValue(id))

    /// 部分类型Id无效，请用别的方法
    member _.TryLookupById(id : 'Id) =
        let ret = col.FindById(new LiteDB.BsonValue(id))
        if isNull (box ret) then
            None
        else
            Some ret

    abstract BuildCollection : unit -> unit

    interface Collections.IEnumerable with
        member x.GetEnumerator() = col.FindAll().GetEnumerator() :> Collections.IEnumerator

    interface Collections.Generic.IEnumerable<'Value> with
        member x.GetEnumerator() = col.FindAll().GetEnumerator()
    
    interface IXivDataSource

let InitAllDb() = 
    Assembly.GetExecutingAssembly().GetTypes()
    |> Array.filter (fun t -> (typeof<IXivDataSource>.IsAssignableFrom(t) && (not (t.IsAbstract))))
    |> Array.iter (fun t -> 
        printfn "%A" t
        let p = t.GetProperty("Instance", BindingFlags.Public ||| BindingFlags.Static)
        let o = p.GetValue(null)
        t.GetMethod("BuildCollection").Invoke(o, null) |> ignore
    )