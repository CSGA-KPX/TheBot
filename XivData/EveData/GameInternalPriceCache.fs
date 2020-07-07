namespace BotData.EveData.GameInternalPriceCache

open System
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open BotData.Common.Database

open BotData.EveData.Utils
open BotData.EveData.EveType

type GameInternalPrice = 
    {
        [<LiteDB.BsonId(false)>]
        [<JsonProperty("type_id")>]
        Id : int
        AdjustedPrice : float
        AveragePrice : float
    }

type GameInternalPriceCollection private () = 
    inherit CachedTableCollection<int, GameInternalPrice>()

    static let instance = GameInternalPriceCollection()

    static member Instance = instance

    override x.IsExpired = (DateTimeOffset.Now - x.GetLastUpdateTime()) >= TimeSpan.FromDays(1.0)

    override x.Depends = [| typeof<EveTypeCollection> |]

    override x.InitializeCollection() =
        let url = "https://esi.evepc.163.com/latest/markets/prices/?datasource=serenity"
        x.Logger.Info(sprintf "Fetching %s" url)
        let json = hc
                    .GetStringAsync(url)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult()
        JArray.Parse(json).ToObject<GameInternalPrice[]>()
        |> x.DbCollection.InsertBulk
        |> ignore