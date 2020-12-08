namespace BotData.EveData.MarketHistoryCache

open System
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open BotData.Common.Database
open BotData.Common.Network

open BotData.EveData.Utils
open BotData.EveData.EveType

[<CLIMutable>]
type MarketHistoryRecord = 
    {
        Average : float
        Date    : DateTimeOffset
        Highest : float
        Lowest  : float
        OrderCount: int
        Volume  : int64
    }

[<CLIMutable>]
type MarketTradeHistory = 
    {
        [<LiteDB.BsonId(false)>]
        Id : int
        History : MarketHistoryRecord []
        Updated : DateTimeOffset
    }

type MarketTradeHistoryCollection private () = 
    inherit CachedItemCollection<int, MarketTradeHistory>()

    static let threshold = TimeSpan.FromDays(2.0)

    static let instance = MarketTradeHistoryCollection()

    static member Instance = instance

    override x.IsExpired (item) = (DateTimeOffset.Now - item.Updated) >= threshold

    override x.Depends = [| typeof<EveTypeCollection> |]

    override x.FetchItem(itemId) =
        let url =
            sprintf "https://esi.evepc.163.com/latest/markets/10000002/history/?datasource=serenity&type_id=%i" itemId
        x.Logger.Info(sprintf "Fetching %s" url)
        let json = hc
                    .GetStringAsync(url)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult()

        let history = JArray.Parse(json).ToObject<MarketHistoryRecord[]>()
        {
            Id = itemId
            History = history
            Updated = DateTimeOffset.Now
        }