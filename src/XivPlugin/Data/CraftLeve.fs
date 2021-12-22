namespace KPX.XivPlugin.Data.CraftLeve

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb
open KPX.TheBot.Host.DataModel.Recipe
open KPX.TheBot.Host.Utils.RecipeRPN


open KPX.XivPlugin.Data.ClassJobMapping

open LiteDB


[<CLIMutable>]
type CraftLeveInfo =
    { [<BsonId(false)>]
      Id: int
      Level: int
      Repeats: int
      Items: RecipeMaterial<int> []
      ClassJob: string
      GilReward: int }

type CraftLeveInfoCollection private () =
    inherit CachedTableCollection<CraftLeveInfo>()
    static let instance = CraftLeveInfoCollection()
    static member Instance = instance

    override x.Depends = Array.singleton typeof<ClassJobMappingCollection>

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection

        db.EnsureIndex(BsonExpression.Create("ClassJob")) |> ignore

        let col = KPX.XivPlugin.Data.XivProvider.XivCollectionChs

        seq {
            let acc = ItemAccumulator<_>()

            for row in col.CraftLeve.TypedRows do
                acc.Clear()

                Array.iter2
                    (fun item count ->
                        if item <> 0 then
                            acc.Update(item, count))
                    (row.Item.AsInts())
                    (row.ItemCount.AsDoubles())

                let repeats = row.Repeats.AsInt() + 1
                let leve = row.Leve.AsRow()
                let leveLevel = leve.ClassJobLevel.AsInt()
                let leveJobCat = leve.ClassJobCategory.AsRow()
                let leveJobCatKey = leveJobCat.Key.Main
                let gil = leve.GilReward.AsInt()

                if leveJobCatKey >= 8 && leveJobCatKey <= 15 then
                    let job =
                        ClassJobMappingCollection
                            .Instance
                            .SearchByName(
                                leveJobCat.Name.AsString()
                            )
                            .Value

                    yield
                        { Id = row.Key.Main
                          Level = leveLevel
                          Repeats = repeats
                          Items = acc.AsMaterials()
                          ClassJob = job
                          GilReward = gil }
        }
        |> db.InsertBulk
        |> ignore

    member x.GetByClassJob(job: ClassJobMapping) =
        let query = Query.EQ("ClassJob", BsonValue(job.Value))

        x.DbCollection.Find(query) |> Seq.toArray
