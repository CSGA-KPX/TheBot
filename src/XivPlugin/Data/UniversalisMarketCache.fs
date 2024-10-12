namespace KPX.XivPlugin.Data

open System
open System.Collections.Generic

open KPX.TheBot.Host.Network
open KPX.TheBot.Host.DataCache

open KPX.XivPlugin.Data

open LiteDB

open Newtonsoft.Json
open Newtonsoft.Json.Linq


[<AutoOpen>]
module private Utils =
    let fields =
        [ "itemID"
          "lastUploadTime"
          "listings.quantity"
          "listings.hq"
          "listings.pricePerUnit"
          "recentHistory.hq"
          "recentHistory.pricePerUnit"
          "recentHistory.quantity"
          "recentHistory.timestamp" ]

    let fetchFields = String.Join(',', fields)

    let fetchFieldsMulti = String.Join(',', fields |> Seq.map (fun field -> $"items.{field}"))

exception UniversalisAccessException of Net.Http.HttpResponseMessage

type MarketInfo =
    { World: World
      Item: XivItem }

    override x.ToString() =
        $"%i{x.World.WorldId}/%i{x.Item.ItemId}"

    static member FromString(str: string) =
        let ret = str.Split('/')
        let world = World.GetWorldById(ret.[0] |> int)
        let iid = ret.[1] |> int

        { World = world
          Item = ItemCollection.Instance.GetByItemId(iid) }

[<CLIMutable>]
type MarketOrder =
    { PricePerUnit: int
      Quantity: int
      [<JsonProperty("hq")>]
      IsHQ: bool }

[<CLIMutable>]
type TradeLog =
    { [<JsonProperty("hq")>]
      IsHQ: bool
      PricePerUnit: int
      Quantity: int
      TimeStamp: int64 }

[<CLIMutable>]
type MarketCache =
    {
        [<BsonId(false)>]
        Id: string
        /// 本地最后获取时间
        LastFetchTime: DateTimeOffset
        /// Universalis上的最后更新时间
        LastUploadTime: DateTimeOffset
        Listings: MarketOrder[]
        TradeLogs: TradeLog[]
    }

    member x.GetInfo() = MarketInfo.FromString(x.Id)

[<Struct>]
[<RequireQualifiedAccess>]
type private MarketData =
    | Order of odrItem: MarketOrder
    | Trade of logItem: TradeLog

    member x.IsHq =
        match x with
        | Order x -> x.IsHQ
        | Trade x -> x.IsHQ

    member x.Quantity =
        match x with
        | Order x -> x.Quantity
        | Trade x -> x.Quantity

    member x.Price =
        match x with
        | Order x -> x.PricePerUnit
        | Trade x -> x.PricePerUnit

type UniversalisAnalyzer internal (record: MarketCache) =

    let info = record.GetInfo()

    let listings = record.Listings |> Array.map MarketData.Order

    let tradelogs = record.TradeLogs |> Array.map MarketData.Trade

    let takeHq (data: MarketData[]) = data |> Array.filter (fun x -> x.IsHq)

    let takeSample (data: MarketData[]) =
        [| let marketSamplePercent = 25
           let samples = data |> Array.sortBy (fun x -> x.Price)

           let itemCount = data |> Array.sumBy (fun x -> x.Quantity)

           let cutLen = itemCount * marketSamplePercent / 100
           let mutable rest = cutLen

           match itemCount = 0, cutLen = 0 with
           | true, _ -> ()
           | false, true ->
               //返回第一个
               yield data.[0]
           | false, false ->
               for record in samples do
                   let takeCount = min rest record.Quantity

                   if takeCount <> 0 then
                       rest <- rest - takeCount
                       yield record |]

    let weightedPrice (data: MarketData[]) =
        let mutable sum = 0
        let mutable quantity = 0

        for d in data do
            sum <- sum + (d.Price * d.Quantity)
            quantity <- quantity + d.Quantity

        (float sum) / (float quantity)

    member x.Item = info.Item

    member x.World = info.World

    member x.ListingAllSampledPrice() = listings |> takeSample |> weightedPrice

    member x.ListingHqSampledPrice() =
        listings |> takeHq |> takeSample |> weightedPrice

    member x.TradelogAllPrice() = tradelogs |> weightedPrice

    member x.TradeLogHqPrice() = tradelogs |> takeHq |> weightedPrice

    /// 按照加权订单价格->交易价格->NaN进行排序
    member x.AllPrice() =
        if listings.Length <> 0 then x.ListingAllSampledPrice()
        elif tradelogs.Length <> 0 then x.TradelogAllPrice()
        else 0.0

    member x.LastUpdated = record.LastUploadTime


[<CLIMutable>]
type UniversalisResultItem =
    {
        ItemId: int
        /// Universalis上的最后更新时间
        LastUploadTime: int64
        Listings: MarketOrder[]
        RecentHistory: TradeLog[]
    }

    member x.ConvertToMarketCache(world: World, itemSource: IReadOnlyDictionary<int, XivItem>) =
        { Id =
            { World = world
              Item = itemSource.[x.ItemId] }
                .ToString()
          LastFetchTime = DateTimeOffset.Now
          LastUploadTime = DateTimeOffset.FromUnixTimeMilliseconds(x.LastUploadTime)
          Listings = x.Listings
          TradeLogs = x.RecentHistory }

[<CLIMutable>]
type UniversalisResultMulti =
    { Items: Dictionary<int, UniversalisResultItem> }

type MarketInfoCollection private () =
    inherit CachedItemCollection<string, MarketCache>()

    static let threshold = TimeSpan.FromHours(2.0)

    let generateUntradable (info: MarketInfo) =
        let time = DateTimeOffset.Now

        { Id = info.ToString()
          LastFetchTime = time
          LastUploadTime = time
          Listings = Array.empty
          TradeLogs = Array.empty }

    static member val Instance = MarketInfoCollection()

    override x.IsExpired(item) =
        (DateTimeOffset.Now - item.LastFetchTime) >= threshold

    override x.Depends = Array.singleton typeof<ItemCollection>

    member x.GetMarketInfo(world: World, item: XivItem) =
        let info = { World = world; Item = item }
        x.GetItem(info.ToString()) |> UniversalisAnalyzer

    /// 批量获取价格信息，按Dictionary类型返回结果
    member x.DoFetchBatch(world: World, items: XivItem[]) =
        let dict = Dictionary<XivItem, MarketCache>()

        if items.Length > 100 then
            for chunk in Array.chunkBySize 100 items do
                for kv: KeyValuePair<XivItem, MarketCache> in x.DoFetchBatch(world, chunk) do
                    dict.TryAdd(kv.Key, kv.Value) |> ignore

        // 重写items，去掉不能访问的部分
        let items =
            [ for item in items do
                  if item.IsUntradable then
                      dict.Add(item, generateUntradable ({ World = world; Item = item }))
                  else
                      yield item.ItemId, item ]
            |> readOnlyDict

        let isMulti = items.Count > 1

        let resp =
            let fetchUrl =
                let ids = String.Join(',', items.Values |> Seq.map (fun item -> item.ItemId))
                let fields = if isMulti then fetchFieldsMulti else fetchFields
                $"https://universalis.app/api/v2/%i{world.WorldId}/{ids}?listings=20&entries=20&fields={fields}"


            let rec fetchWithRetry url retry =
                let resp = TheBotWebFetcher.fetch url

                if not resp.IsSuccessStatusCode then
                    x.Logger.Warn $"Universalis返回错误%s{resp.ReasonPhrase}: {url}"

                    if retry > 1 then
                        fetchWithRetry url (retry - 1)
                    else
                        x.Logger.Error $"重试次数用尽: {url}"
                        None
                else
                    let obj =
                        use stream = resp.Content.ReadAsStream()
                        use reader = new IO.StreamReader(stream)
                        use jsonReader = new JsonTextReader(reader)
                        JObject.Load(jsonReader)

                    Some obj

            fetchWithRetry fetchUrl 3

        if resp.IsNone then

            let time = DateTimeOffset.MinValue

            for item in items.Values do
                dict.Add(
                    item,

                    { Id = { World = world; Item = item }.ToString()
                      LastFetchTime = time
                      LastUploadTime = time
                      Listings = Array.empty
                      TradeLogs = Array.empty }
                )
        else
            let results =
                [ let obj = resp.Value

                  if isMulti then
                      yield! (obj.ToObject<UniversalisResultMulti>().Items.Values)
                  else
                      yield (obj.ToObject<UniversalisResultItem>()) ]

            for result in results do
                let xivItem = items.[result.ItemId]
                let cache = result.ConvertToMarketCache(world, items)
                dict.Add(xivItem, cache)

        dict.AsReadOnly()

    /// 批量获取到数据库
    member x.LoadBatch(world: World, items: XivItem[]) =
        let result = x.DoFetchBatch(world, items)
        x.LoadItems(result.Values)

    override x.DoFetchItem(info) =
        let info = MarketInfo.FromString(info)

        let ret = x.DoFetchBatch(info.World, Array.singleton info.Item)
        ret.[info.Item]
