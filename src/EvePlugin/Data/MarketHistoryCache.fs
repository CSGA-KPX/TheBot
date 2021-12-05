namespace KPX.EvePlugin.Data.MarketHistoryCache

open System
open Newtonsoft.Json.Linq

open KPX.TheBot.Host
open KPX.TheBot.Host.DataCache

open KPX.EvePlugin.Data.EveType


[<CLIMutable>]
type MarketHistoryRecord =
    { Average: float
      Date: DateTimeOffset
      Highest: float
      Lowest: float
      OrderCount: int
      Volume: int64 }

[<CLIMutable>]
type MarketTradeHistory =
    { [<LiteDB.BsonId(false)>]
      Id: int
      History: MarketHistoryRecord []
      Updated: DateTimeOffset }

type MarketTradeHistoryCollection private () =
    inherit CachedItemCollection<int, MarketTradeHistory>()

    static let threshold = TimeSpan.FromDays(2.0)

    static let instance = MarketTradeHistoryCollection()

    static member Instance = instance

    override x.IsExpired(item) =
        (DateTimeOffset.Now - item.Updated) >= threshold

    override x.Depends = [| typeof<EveTypeCollection> |]

    override x.DoFetchItem(itemId) =
        let url = $"https://esi.evepc.163.com/latest/markets/10000002/history/?datasource=serenity&type_id=%i{itemId}"

        x.Logger.Info $"Fetching %s{url}"

        let json =
            Network
                .HttpClient
                .GetStringAsync(url)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()

        let history =
            JArray
                .Parse(json)
                .ToObject<MarketHistoryRecord []>()

        { Id = itemId
          History = history
          Updated = DateTimeOffset.Now }
