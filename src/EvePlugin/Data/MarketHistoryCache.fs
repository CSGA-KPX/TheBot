namespace KPX.EvePlugin.Data.MarketHistoryCache

open System
open Newtonsoft.Json
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
        let url =
            $"https://esi.evepc.163.com/latest/markets/10000002/history/?datasource=serenity&type_id=%i{itemId}"

        let resp = TheBotWebFetcher.fetch url
        use stream = resp.Content.ReadAsStream()
        use reader = new IO.StreamReader(stream)
        use jsonReader = new JsonTextReader(reader)

        let history =
            JArray
                .Load(jsonReader)
                .ToObject<MarketHistoryRecord []>()

        { Id = itemId
          History = history
          Updated = DateTimeOffset.Now }
