namespace KPX.TheBot.Data.XivData.CraftLeve

open KPX.TheBot.Data.Common.Database
open KPX.TheBot.Data.CommonModule.Recipe

open KPX.TheBot.Data.XivData.ClassJobMapping

open LiteDB


[<CLIMutable>]
type CraftLeveInfo =
    { [<BsonId(false)>]
      Id : int
      Level : int
      Repeats : int
      Items : RecipeMaterial<int> []
      ClassJob : string
      GilReward : int}

type CraftLeveInfoCollection private () =
    inherit CachedTableCollection<string, CraftLeveInfo>(DefaultDB)
    static let instance = CraftLeveInfoCollection()
    static member Instance = instance
    
    override x.Depends =
        Array.singleton typeof<ClassJobMappingCollection>

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection

        db.EnsureIndex(LiteDB.BsonExpression.Create("ClassJob"))
        |> ignore

        use col = BotDataInitializer.XivCollectionChs

        seq {
            for row in col.GetSheet("CraftLeve") do
                let repeats = row.As<int>("Repeats") + 1

                let acc = ItemAccumulator<_>()
                let items = row.AsArray<int>("Item", 3)
                let counts = row.AsArray<float>("ItemCount", 3)
                for idx = 0 to 2 do
                    if items.[idx] <> 0 then acc.Update(items.[idx], counts.[idx])

                let leve = row.AsRow("Leve")
                let level = leve.As<int>("ClassJobLevel")
                let jobCat = leve.AsRow("ClassJobCategory")
                let jobCatKey = jobCat.Key.Main
                let gil = leve.As<int>("GilReward")
                if jobCatKey >= 8 && jobCatKey <= 15 then
                    let job =
                        ClassJobMappingCollection
                            .Instance
                            .SearchByName(jobCat.As<string>("Name"))
                            .Value

                    yield
                        { Id = row.Key.Main
                          Level = level
                          Repeats = repeats
                          Items = acc.AsMaterials()
                          ClassJob = job 
                          GilReward = gil}
        }
        |> db.InsertBulk
        |> ignore

    member x.GetById (id : int) = x.DbCollection.SafeFindById(id)

    member x.GetByClassJob (job : ClassJobMapping) =
        let query = Query.EQ("ClassJob", BsonValue(job.Value))
        x.DbCollection.Find(query)
        |> Seq.toArray