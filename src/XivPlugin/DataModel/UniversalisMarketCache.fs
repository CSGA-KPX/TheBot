namespace KPX.XivPlugin.DataModel

open System

open KPX.TheBot.Host.Network
open KPX.TheBot.Host.DataCache

open KPX.XivPlugin.DataModel

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
          Item = ItemCollection.Instance.GetByItemId(iid, world.VersionRegion) }

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
      /// Id为字符串化的MarketInfo
      Id: string
      /// 本地最后获取时间
      LastFetchTime: DateTimeOffset
      /// Universalis上的最后更新时间
      LastUploadTime: DateTimeOffset
      Listings: MarketOrder []
      TradeLogs: TradeLog [] }

    member x.GetInfo() = MarketInfo.FromString(x.Id)

type MarketInfoCollection private () =
    inherit CachedItemCollection<string, UniversalisRecord>()

    static let threshold = TimeSpan.FromHours(2.0)

    static let instance = MarketInfoCollection()

    static member Instance = instance

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
