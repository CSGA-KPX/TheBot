namespace KPX.XivPlugin.DataModel

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb

open KPX.XivPlugin
open KPX.XivPlugin.DataModel

open LiteDB


[<CLIMutable>]
type XivContent =
    { [<BsonId>]
      LiteDbId: int
      Region: VersionRegion
      ContentId: int
      Name: string
      IsDailyFrontlineChallengeRoulette: bool }

type XivContentCollection private () =
    inherit CachedTableCollection<XivContent>()

    static member val Instance = XivContentCollection()

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        x.DbCollection.EnsureIndex(fun x -> x.Region) |> ignore

        seq {
            let col = ChinaDistroData.GetCollection()

            for row in col.ContentFinderCondition.TypedRows do
                yield
                    { LiteDbId = 0
                      Region = VersionRegion.China
                      ContentId = row.Key.Main
                      Name = row.Name.AsString()
                      IsDailyFrontlineChallengeRoulette = row.DailyFrontlineChallenge.AsBool() }

            let col = OfficalDistroData.GetCollection()

            for row in col.ContentFinderCondition.TypedRows do
                yield
                    { LiteDbId = 0
                      Region = VersionRegion.Offical
                      ContentId = row.Key.Main
                      Name = row.Name.AsString()
                      IsDailyFrontlineChallengeRoulette = row.DailyFrontlineChallenge.AsBool() }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.GetDailyFrontline(region) =
        x.DbCollection.QueryAllArray(fun x -> x.Region = region && x.IsDailyFrontlineChallengeRoulette)

    interface IDataTest with
        member x.RunTest() =
            x.GetDailyFrontline(VersionRegion.China)
            |> Array.exists (fun x -> x.Name.Contains("尘封秘岩"))
            |> Expect.isTrue

            x.GetDailyFrontline(VersionRegion.Offical)
            |> Array.exists (fun x -> x.Name.Contains("シールロック"))
            |> Expect.isTrue
