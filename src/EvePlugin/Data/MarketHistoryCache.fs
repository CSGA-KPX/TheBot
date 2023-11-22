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
      History: MarketHistoryRecord[]
      Updated: DateTimeOffset }

    // 因为ESI只返回有交易记录的数据
    // 所以需要人工把没有交易记录的日子补上
    member x.CalculateRealTradeVolume() =
        if x.History.Length = 0 then
            0.0
        else
            let earliest = x.History |> Array.minBy (fun x -> x.Date)
            let daysElapsed = floor (DateTimeOffset.Now - earliest.Date).TotalDays
            let volume = x.History |> Array.sumBy (fun x -> x.Volume) |> float

            volume / daysElapsed

type MarketTradeHistoryCollection private () =
    inherit CachedItemCollection<int, MarketTradeHistory>()

    static let threshold = TimeSpan.FromDays(2.0)

    static let instance = MarketTradeHistoryCollection()

    static member Instance = instance

    override x.IsExpired(item) =
        (DateTimeOffset.Now - item.Updated) >= threshold

    override x.Depends = [| typeof<EveTypeCollection> |]

    override x.DoFetchItem(itemId) =
        let item = EveTypeCollection.Instance.GetById(itemId)

        if item.MarketGroupId = 0 then

            { Id = itemId
              History = Array.empty<_>
              Updated = DateTimeOffset.Now }
        else

            let url =
                $"https://esi.evepc.163.com/latest/markets/10000002/history/?datasource=serenity&type_id=%i{itemId}"

            let resp = TheBotWebFetcher.fetch url
            use stream = resp.Content.ReadAsStream()
            use reader = new IO.StreamReader(stream)
            use jsonReader = new JsonTextReader(reader)

            let history = JArray.Load(jsonReader).ToObject<MarketHistoryRecord[]>()

            { Id = itemId
              History = history
              Updated = DateTimeOffset.Now }
