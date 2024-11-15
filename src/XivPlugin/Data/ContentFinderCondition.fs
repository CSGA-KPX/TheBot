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

        // CFC变动比较大的表，使用动态计算的方式来获得相关资料
        // PvP这一列基本固定，不会改变

        seq {
            let col = ChinaDistroData.GetCollection()
            let cfc = col.ContentFinderCondition
            cfc.ResetToCsvHeader()

            let contentNameCol =
                [ // 从5开始总没错
                  for i = 5 to cfc.Header.Headers.Count - 1 do
                      let index = HeaderIndex i

                      if cfc.Header.GetTypedFieldType(index) = XivCellType.String then
                          yield index ]
                |> List.head // 目前只有一列

            let dfcContentCol =
                let lastNumberIdx =
                    seq { cfc.Header.Headers.Count - 1 .. -1 .. 0 }
                    |> Seq.find (fun index ->
                        let idx = HeaderIndex index
                        cfc.Header.GetTypedFieldType(idx) = XivCellType.Number)
                // +1 byte
                // +6 LevelingRoulette	HighLevelRoulette	MSQRoulette	GuildHestRoulette	ExpertRoulette	TrialRoulette
                HeaderIndex(lastNumberIdx + 1 + 6)

            let dfc =
                col.ContentFinderCondition
                |> Seq.filter (fun row -> row.PvP.AsBool() && row.As<bool>(dfcContentCol))

            for row in dfc do
                yield
                    { LiteDbId = 0
                      Region = VersionRegion.China
                      ContentId = row.Key.Main
                      Name = row.As<string>(contentNameCol)
                      IsDailyFrontlineChallengeRoulette = true }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

        seq {
            let col = OfficalDistroData.GetCollection()
            let cfc = col.ContentFinderCondition
            cfc.InterferenceHeader()
            let contentNameCol =
                [ // 从5开始总没错
                  for i = 5 to cfc.Header.Headers.Count - 1 do
                      let index = HeaderIndex i
                      if cfc.Header.GetTypedFieldType(index) = XivCellType.String then
                          yield index ]
                |> List.head // 目前只有一列

            let dfcContentCol =
                let lastNumberIdx =
                    seq { cfc.Header.Headers.Count - 1 .. -1 .. 0 }
                    |> Seq.find (fun index ->
                        let idx = HeaderIndex index
                        cfc.Header.GetTypedFieldType(idx) = XivCellType.Number)
                // +1 byte
                // +6 LevelingRoulette	HighLevelRoulette	MSQRoulette	GuildHestRoulette	ExpertRoulette	TrialRoulette
                HeaderIndex(lastNumberIdx + 1 + 6)

            let dfc =
                col.ContentFinderCondition
                |> Seq.filter (fun row -> row.PvP.AsBool() && row.As<bool>(dfcContentCol))

            for row in dfc do
                yield
                    { LiteDbId = 0
                      Region = VersionRegion.Offical
                      ContentId = row.Key.Main
                      Name = row.As<string>(contentNameCol)
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
