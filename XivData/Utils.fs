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
    Db.Shrink()

[<AbstractClass>]
type XivDataSource() = 
    class
    end

let InitAllDb() = 
    Assembly.GetExecutingAssembly().GetTypes()
    |> Array.filter (fun t -> (t.IsSubclassOf(typeof<XivDataSource>) && (not (t.IsAbstract))))
    |> Array.iter (fun t -> 
        printfn "%A" t
        let p = t.GetProperty("Instance", BindingFlags.Public ||| BindingFlags.Static)
        p.GetValue(null) |> ignore
    )