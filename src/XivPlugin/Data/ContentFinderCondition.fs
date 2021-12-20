module KPX.XivPlugin.Data.ContentFinderCondition

open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb

open LiteDB


[<CLIMutable>]
type XivContent =
    { [<BsonId(false)>]
      Id: int
      Name: string
      IsDailyFrontlineChallengeRoulette: bool }

type XivContentCollection private () =
    inherit CachedTableCollection<XivContent>()

    static let instance = XivContentCollection()
    static member Instance = instance

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let col = XivProvider.XivCollectionChs

        seq {
            for row in col.ContentFinderCondition.TypedRows do
                yield
                    { Id = row.Key.Main
                      Name = row.Name.AsString()
                      // 5aa2afd1eb073f128be578f1b78e2b717f81a5be数据错误。
                      IsDailyFrontlineChallengeRoulette = row.TrialRoulette.AsBool() }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.GetAll() = x.DbCollection.FindAll()
