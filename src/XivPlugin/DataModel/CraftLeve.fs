namespace KPX.XivPlugin.DataModel

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb
open KPX.TheBot.Host.DataModel.Recipe

open KPX.XivPlugin
open KPX.XivPlugin.DataModel

open LiteDB


[<CLIMutable>]
type CraftLeveInfo =
    { [<BsonId>]
      LiteDbId: int
      Region: VersionRegion
      LeveId: int
      LeveLevel: int
      Repeats: int
      Items: RecipeMaterial<int> []
      ClassJob: string
      GilReward: int }

type CraftLeveInfoCollection private () =
    inherit CachedTableCollection<CraftLeveInfo>()

    static member val Instance = CraftLeveInfoCollection()

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        x.DbCollection.EnsureIndex(BsonExpression.Create("ClassJob")) |> ignore

        x.InitChs()
        x.InitOffical()

    member x.InitChs() =
        seq {
            let col = ChinaDistroData.GetCollection()
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
                    let job = ClassJobMapping.Parse(leveJobCat.Name.AsString())

                    yield
                        { LiteDbId = 0
                          Region = VersionRegion.China
                          LeveId = row.Key.Main
                          LeveLevel = leveLevel
                          Repeats = repeats
                          Items = acc.AsMaterials()
                          ClassJob = job.Value
                          GilReward = gil }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.InitOffical() =
        seq {
            let col = OfficalDistroData.GetCollection()
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
                    let job = ClassJobMapping.Parse(leveJobCat.Name.AsString())

                    yield
                        { LiteDbId = 0
                          Region = VersionRegion.Offical
                          LeveId = row.Key.Main
                          LeveLevel = leveLevel
                          Repeats = repeats
                          Items = acc.AsMaterials()
                          ClassJob = job.Value
                          GilReward = gil }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    /// <summary>
    /// 
    /// </summary>
    /// <param name="job"></param>
    /// <param name="region"></param>
    member x.GetByClassJob(job: ClassJob, region) =
        let job = job.Value
        x.DbCollection.QueryAllArray(fun x -> x.ClassJob = job && x.Region = region)
