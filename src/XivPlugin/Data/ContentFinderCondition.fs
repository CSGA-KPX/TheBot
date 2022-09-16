namespace KPX.XivPlugin.Data

open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb

open KPX.XivPlugin.Data

open LibFFXIV.GameData.Raw

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

            let c =
                col.ContentFinderCondition
                |> Seq.filter (fun row -> row.PvP.AsBool())
                |> Seq.cache

            let colRange =
                let row = (Seq.head c)
                let colMid = row.DailyFrontlineChallenge.Index.ToHdrIndex
                [ colMid - 2 .. colMid + 2 ]

            let dfcCol =
                colRange
                |> List.choose (fun idx ->
                    let idx = HeaderIndex idx

                    if c |> Seq.exists (fun row -> row.As<bool>(idx)) then
                        Some idx
                    else
                        None)
                |> List.head

            for row in c do
                if row.As<bool>(dfcCol) = true then
                    yield
                        { LiteDbId = 0
                          Region = VersionRegion.China
                          ContentId = row.Key.Main
                          Name = row.Name.AsString()
                          IsDailyFrontlineChallengeRoulette = true }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

        seq {
            let col = OfficalDistroData.GetCollection()

            let c =
                col.ContentFinderCondition
                |> Seq.filter (fun row -> row.PvP.AsBool())
                |> Seq.cache

            let colRange =
                let row = (Seq.head c)
                let colMid = row.DailyFrontlineChallenge.Index.ToHdrIndex
                [ colMid - 2 .. colMid + 2 ]

            let dfcCol =
                colRange
                |> List.choose (fun idx ->
                    let idx = HeaderIndex idx

                    if c |> Seq.exists (fun row -> row.As<bool>(idx)) then
                        Some idx
                    else
                        None)
                |> List.head

            for row in c do
                if row.As<bool>(dfcCol) = true then
                    yield
                        { LiteDbId = 0
                          Region = VersionRegion.Offical
                          ContentId = row.Key.Main
                          Name = row.Name.AsString()
                          IsDailyFrontlineChallengeRoulette = true }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

        (x :> IDataTest).RunTest()

    member x.GetDailyFrontline(region: VersionRegion) =
        Query.And(Query.EQ("Region", region.BsonValue), Query.EQ("IsDailyFrontlineChallengeRoulette", true))
        |> x.DbCollection.Find
        |> Seq.toArray
        |> Array.sortBy (fun ctx -> ctx.ContentId)

    interface IDataTest with
        member x.RunTest() =
            x.GetDailyFrontline(VersionRegion.China)
            |> Array.exists (fun x -> x.Name.Contains("尘封秘岩"))
            |> Expect.isTrue

            x.GetDailyFrontline(VersionRegion.Offical)
            |> Array.exists (fun x -> x.Name.Contains("シールロック"))
            |> Expect.isTrue
