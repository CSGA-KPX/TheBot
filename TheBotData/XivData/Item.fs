namespace KPX.TheBot.Data.XivData

open KPX.TheBot.Data.Common.Database


[<CLIMutable>]
type XivItem =
    { [<LiteDB.BsonId(false)>]
      Id : int
      Name : string }

    override x.ToString() = $"%s{x.Name}(%i{x.Id})"

    static member GetUnknown() = { Id = -1; Name = "Unknown" }


type ItemCollection private () =
    inherit CachedTableCollection<int, XivItem>(DefaultDB)

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

        let col = BotDataInitializer.XivCollectionChs

        seq {
            for row in col.Item.TypedRows do
                yield
                    { Id = row.Key.Main
                      Name = row.Name.AsString() }
        }
        |> db.InsertBulk
        |> ignore

    member x.GetByItemId(id : int) =
        x.PassOrRaise(x.DbCollection.TryFindById(id), "找不到物品:{0}", id)

    member x.TryGetByItemId(id : int) = x.DbCollection.TryFindById(id)

    member x.TryGetByName(name : string) =
        let ret =
            x.DbCollection.FindOne(LiteDB.Query.EQ("Name", new LiteDB.BsonValue(name)))

        if isNull (box ret) then None else Some ret

    member x.SearchByName(str) =
        x.DbCollection.Find(LiteDB.Query.Contains("Name", str))
        |> Seq.toArray
