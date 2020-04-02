module XivData.Item

open System
open LibFFXIV.GameData.Raw

[<CLIMutable>]
type ItemRecord =
    { [<LiteDB.BsonIdAttribute(false)>]
      Id : int
      Name : string }

    override x.ToString() = sprintf "%s(%i)" x.Name x.Id

    static member GetUnknown(lodeId) =
        { Id = -1
          Name = "Unknown" }

type ItemCollection private () =
    inherit Utils.XivDataSource<int, ItemRecord>()

    static let instance = new ItemCollection()
    static member Instance = instance

    override x.BuildCollection() =
        let db = x.Collection
        printfn "Building ItemCollection"
        db.EnsureIndex("_id", true) |> ignore
        db.EnsureIndex("Name") |> ignore

        let chs = 
            let col = EmbeddedXivCollection(XivLanguage.ChineseSimplified, true) :> IXivCollection
            col.GetSheet("Item", [| "Name" |])

        let eng = 
            Utils.GlobalVerCollection.GetSheet("Item", [| "Name" |])

        let merged = Utils.MergeSheet(chs, eng, (fun (a,b) -> a.As<string>("Name") = ""))

        seq {
            for row in merged do
                yield { Id = row.Key.Main
                        Name = row.As<string>("Name") }
        }
        |> db.InsertBulk
        |> ignore
        GC.Collect()


    member x.TryLookupByName(name : string) =
        let ret = x.Collection.FindOne(LiteDB.Query.EQ("Name", new LiteDB.BsonValue(name)))
        if isNull (box ret) then None
        else Some ret

    member x.SearchByName(str) = x.Collection.Find(LiteDB.Query.Contains("Name", str)) |> Seq.toArray

    member x.AllItems() = x.Collection.FindAll() |> Seq.toArray

    interface Utils.IXivDataSource with
        override x.BuildOrder = Int32.MinValue