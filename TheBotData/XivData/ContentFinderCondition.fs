module KPX.TheBot.Data.XivData.ContentFinderCondition

open System

open KPX.TheBot.Data.Common.Database

open LiteDB


[<CLIMutable>]
type XivContent =
    { [<BsonId(false)>]
      Id : int
      Name : string
      IsHighEndDuty : bool
      IsLevelingRoulette : bool
      IsLevel5060Roulette : bool
      IsMSQRoulette : bool
      IsGuildHestRoulette : bool
      IsExpertRoulette : bool
      IsTrialRoulette : bool
      IsDailyFrontlineChallengeRoulette : bool
      IsLevel70Roulette : bool
      IsMentorRoulette : bool }

type XivContentCollection private () =
    inherit CachedTableCollection<int, XivContent>()

    static let instance = XivContentCollection()
    static member Instance = instance

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        use col = BotDataInitializer.XivCollectionChs
        let sht = col.GetSheet("ContentFinderCondition")

        seq {
            for row in sht do
                yield
                    { Id = row.Key.Main
                      Name = row.As<string>("Name")
                      IsHighEndDuty = row.As<bool>("HighEndDuty")
                      IsLevelingRoulette = row.As<bool>("LevelingRoulette")
                      IsLevel5060Roulette = row.As<bool>("Level50/60Roulette")
                      IsMSQRoulette = row.As<bool>("MSQRoulette")
                      IsGuildHestRoulette = row.As<bool>("GuildHestRoulette")
                      IsExpertRoulette = row.As<bool>("ExpertRoulette")
                      IsTrialRoulette = row.As<bool>("TrialRoulette")
                      IsDailyFrontlineChallengeRoulette = row.As<bool>("DailyFrontlineChallenge")
                      IsLevel70Roulette = row.As<bool>("Level70Roulette")
                      IsMentorRoulette = row.As<bool>("MentorRoulette") }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.GetAll() = x.DbCollection.FindAll()
