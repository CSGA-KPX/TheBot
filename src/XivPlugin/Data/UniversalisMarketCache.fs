namespace KPX.XivPlugin.Data

open System

open KPX.TheBot.Host.Network
open KPX.TheBot.Host.DataCache

open KPX.XivPlugin.Data

open LiteDB

open Newtonsoft.Json
open Newtonsoft.Json.Linq


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

type XivCity =
    | LimsaLominsa = 1
    | Gridania = 2
    | Uldah = 3
    | Ishgard = 4
    | Kugane = 7
    | Crystarium = 10

[<CLIMutable>]
type MarketOrder =
    { LastReviewTime: int64
      PricePerUnit: int
      Quantity: int
      CreatorName: string
      [<JsonProperty("hq")>]
      IsHQ: bool
      IsCrafted: bool
      RetainerCity: XivCity
      RetainerName: string
      Total: int64 }

[<CLIMutable>]
type TradeLog =
    { [<JsonProperty("hq")>]
      IsHQ: bool
      PricePerUnit: int
      Quantity: int
      TimeStamp: int64
      BuyerName: string
      Total: int64 }

[<CLIMutable>]
type UniversalisRecord =
    { [<BsonId(false)>]
      Id: string
      /// 本地最后获取时间
      LastFetchTime: DateTimeOffset
      /// Universalis上的最后更新时间
      LastUploadTime: DateTimeOffset
      Listings: MarketOrder []
      TradeLogs: TradeLog [] }

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

type UniversalisAnalyzer internal (record: UniversalisRecord) =

    let info = record.GetInfo()

    let listings = record.Listings |> Array.map MarketData.Order

    let tradelogs = record.TradeLogs |> Array.map MarketData.Trade

    let takeHq (data: MarketData []) = data |> Array.filter (fun x -> x.IsHq)

    let takeSample (data: MarketData []) =
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

    let weightedPrice (data: MarketData []) =
        let mutable sum = 0
        let mutable quantity = 0

        for d in data do
            sum <- sum + (d.Price * d.Quantity)
            quantity <- quantity + d.Quantity

        (float sum) / (float quantity)

    member x.Item = info.Item

    member x.World = info.World

    member x.ListingAllSampledPrice() = listings |> takeSample |> weightedPrice

    member x.ListingHqSampledPrice() = listings |> takeHq |> takeSample |> weightedPrice

    member x.TradelogAllPrice() = tradelogs |> takeSample |> weightedPrice

    member x.TradeLogHqPrice() = tradelogs |> takeHq |> takeSample |> weightedPrice

    /// 按照订单价格->交易价格->NaN进行排序
    member x.AllPrice() =
        if listings.Length <> 0 then
            x.ListingAllSampledPrice()
        elif tradelogs.Length <> 0 then
            x.TradelogAllPrice()
        else
            nan

    member x.LastUpdated = record.LastUploadTime

type MarketInfoCollection private () =
    inherit CachedItemCollection<string, UniversalisRecord>()

    static let threshold = TimeSpan.FromHours(2.0)

    static member val Instance = MarketInfoCollection()

    override x.IsExpired(item) =
        (DateTimeOffset.Now - item.LastFetchTime) >= threshold

    override x.Depends = Array.empty

    override x.DoFetchItem(info) =
        let info = MarketInfo.FromString(info)

        let url = $"https://universalis.app/api/%i{info.World.WorldId}/%i{info.Item.ItemId}"

        x.Logger.Info $"正在访问 %s{url}"

        let resp =
            HttpClient
                .GetAsync(url)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()

        if not resp.IsSuccessStatusCode then
            x.Logger.Warn $"Universalis返回错误%s{resp.ReasonPhrase}:%A{info.World}/%A{info.Item}"

            match resp.StatusCode with
            | Net.HttpStatusCode.NotFound ->
                { Id = info.ToString()
                  LastFetchTime = DateTimeOffset.Now
                  LastUploadTime = DateTimeOffset.Now
                  Listings = Array.empty
                  TradeLogs = Array.empty }
            | _ -> raise <| UniversalisAccessException resp
        else
            let json = resp.Content.ReadAsStringAsync() |> Async.AwaitTask |> Async.RunSynchronously

            let o = JObject.Parse(json)

            let updated = o.["lastUploadTime"].Value<int64>() |> DateTimeOffset.FromUnixTimeMilliseconds

            let listings =
                (o.["listings"] :?> JArray)
                    .ToObject<MarketOrder []>()

            let tradelogs =
                (o.["recentHistory"] :?> JArray)
                    .ToObject<TradeLog []>()

            { Id = info.ToString()
              LastFetchTime = DateTimeOffset.Now
              LastUploadTime = updated
              Listings = listings
              TradeLogs = tradelogs }

    member x.GetMarketInfo(world: World, item: XivItem) =
        let info = { World = world; Item = item }
        x.GetItem(info.ToString())
        |> UniversalisAnalyzer
