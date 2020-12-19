namespace BotData.XivData.Item

open System

open BotData.Common.Database

[<CLIMutable>]
type ItemRecord =
    { [<LiteDB.BsonIdAttribute(false)>]
      Id : int
      Name : string }

    override x.ToString() = sprintf "%s(%i)" x.Name x.Id

    static member GetUnknown() = { Id = -1; Name = "Unknown" }


type ItemCollection private () =
    inherit CachedTableCollection<int, ItemRecord>()

    static let instance = ItemCollection()
    static member Instance = instance

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection

        db.EnsureIndex(LiteDB.BsonExpression.Create("_id"), true)
        |> ignore

        db.EnsureIndex(LiteDB.BsonExpression.Create("Name"))
        |> ignore

        use col = BotDataInitializer.XivCollectionChs
        let chs = col.GetSheet("Item", [| "Name" |])

        seq {
            for row in chs do
                yield
                    { Id = row.Key.Main
                      Name = row.As<string>("Name") }
        }
        |> db.InsertBulk
        |> ignore

    member x.GetByItemId(id : int) =
        x.PassOrRaise(x.TryGetByKey(id), "找不到物品:{0}", id)

    member x.TryGetByItemId(id : int) = x.TryGetByKey(id)

    member x.TryGetByName(name : string) =
        let ret =
            x.DbCollection.FindOne(LiteDB.Query.EQ("Name", new LiteDB.BsonValue(name)))

        if isNull (box ret) then None else Some ret

    member x.SearchByName(str) =
        x.DbCollection.Find(LiteDB.Query.Contains("Name", str))
        |> Seq.toArray
