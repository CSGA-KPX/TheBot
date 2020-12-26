namespace KPX.TheBot.Data.EveData.MarketPriceCache

open System
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open KPX.TheBot.Data.Common.Database
open KPX.TheBot.Data.Common.Network

open KPX.TheBot.Data.EveData.Utils
open KPX.TheBot.Data.EveData.EveType

[<CLIMutable>]
type PriceCache =
    { [<LiteDB.BsonId(false)>]
      Id : int
      Sell : float
      Buy : float
      Updated : DateTimeOffset }

type PriceCacheCollection private () =
    inherit CachedItemCollection<int, PriceCache>()

    static let threshold = TimeSpan.FromHours(2.0)

    static let instance = PriceCacheCollection()

    static member Instance = instance

    override x.IsExpired(item) =
        (DateTimeOffset.Now - item.Updated) >= threshold

    override x.Depends = [| typeof<EveTypeCollection> |]

    override x.FetchItem(itemId) =
        let url =
            sprintf
                @"https://www.ceve-market.org/api/market/region/10000002/system/30000142/type/%i.json"
                itemId

        x.Logger.Info(sprintf "Fetching %s" url)

        let json =
            hc
                .GetStringAsync(url)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()

        let obj = JObject.Parse(json)

        let sellMin =
            (obj.GetValue("sell") :?> JObject)
                .GetValue("min")
                .ToObject<float>()

        let buyMax =
            (obj.GetValue("buy") :?> JObject)
                .GetValue("max")
                .ToObject<float>()

        { Id = itemId
          Sell = sellMin
          Buy = buyMax
          Updated = DateTimeOffset.Now }
