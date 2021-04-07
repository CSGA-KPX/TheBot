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
      IsLevel80Roulette : bool
      IsMentorRoulette : bool }

type XivContentCollection private () =
    inherit CachedTableCollection<int, XivContent>(DefaultDB)

    static let instance = XivContentCollection()
    static member Instance = instance

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let col = BotDataInitializer.XivCollectionChs
        seq {
            for row in col.ContentFinderCondition.TypedRows do
                yield
                    { Id = row.Key.Main
                      Name = row.Name.AsString()
                      IsHighEndDuty = row.HighEndDuty.AsBool()
                      IsLevelingRoulette = row.LevelingRoulette.AsBool()
                      IsLevel5060Roulette = row.``Level50/60/70Roulette``.AsBool()
                      IsMSQRoulette = row.MSQRoulette.AsBool()
                      IsGuildHestRoulette = row.GuildHestRoulette.AsBool()
                      IsExpertRoulette = row.ExpertRoulette.AsBool()
                      IsTrialRoulette = row.TrialRoulette.AsBool()
                      IsDailyFrontlineChallengeRoulette = row.DailyFrontlineChallenge.AsBool()
                      IsLevel80Roulette = row.Level80Roulette.AsBool()
                      IsMentorRoulette = row.MentorRoulette.AsBool() }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.GetAll() = x.DbCollection.FindAll()
