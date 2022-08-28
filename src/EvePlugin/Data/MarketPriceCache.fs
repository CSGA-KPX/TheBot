namespace KPX.EvePlugin.Data.MarketPriceCache

open System

open Newtonsoft.Json

open KPX.TheBot.Host
open KPX.TheBot.Host.DataCache

open KPX.EvePlugin.Data.EveType


[<CLIMutable>]
type internal PriceInfo =
    { [<JsonProperty>]
      Max: float
      [<JsonProperty>]
      Min: float
      [<JsonProperty>]
      Volume: uint64 }

[<CLIMutable>]
type internal MarketInfo =
    { [<JsonProperty>]
      All: PriceInfo
      [<JsonProperty>]
      Buy: PriceInfo
      [<JsonProperty>]
      Sell: PriceInfo }

[<CLIMutable>]
type PriceCache =
    { [<LiteDB.BsonId(false)>]
      Id: int
      Sell: float
      Buy: float
      Updated: DateTimeOffset }

type PriceCacheCollection private () =
    inherit CachedItemCollection<int, PriceCache>()

    static let threshold = TimeSpan.FromHours(2.0)

    static let instance = PriceCacheCollection()

    static member Instance = instance

    override x.IsExpired(item) =
        (DateTimeOffset.Now - item.Updated) >= threshold

    override x.Depends = [| typeof<EveTypeCollection> |]

    override x.DoFetchItem(itemId) =
        let url =
            $@"https://www.ceve-market.org/api/market/region/10000002/system/30000142/type/%i{itemId}.json"

        let resp = TheBotWebFetcher.fetch url
        use stream = resp.Content.ReadAsStream()
        use reader = new IO.StreamReader(stream)
        use jsonReader = new JsonTextReader(reader)

        let info =
            JsonSerializer()
                .Deserialize<MarketInfo>(jsonReader)

        let sellMin =
            if info.Sell.Volume = 0UL then
                nan
            else
                info.Sell.Min

        let buyMax =
            if info.Buy.Volume = 0UL then
                nan
            else
                info.Buy.Max

        { Id = itemId
          Sell = sellMin
          Buy = buyMax
          Updated = DateTimeOffset.Now }
