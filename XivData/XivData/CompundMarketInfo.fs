module BotData.XivData.CompundMarketInfo


open System

open Newtonsoft.Json.Linq

open LiteDB

open LibDmfXiv.Shared.MarketOrder
open LibDmfXiv.Shared.TradeLog
open LibDmfXiv.Client

open BotData.Common.Database
open BotData.Common.Network

open BotData.XivData

[<CLIMutable>]
type MarketInfo = 
    {
        World : World.World
        Item  : Item.ItemRecord
    }

    override x.ToString() = 
        sprintf "%i/%i" x.World.WorldId x.Item.Id

    static member FromString(str : string) = 
        let ret = str.Split('/')
        let wid = ret.[0] |> uint16
        let iid = ret.[1] |> int
        {   World = World.WorldFromId.[wid]
            Item = Item.ItemCollection.Instance.GetByItemId(iid)    }


[<CLIMutable>]
type UniversalisRecord = 
    {
        [<BsonId(false)>]
        Id : string
        /// 本地最后获取时间
        LastFetchTime : DateTimeOffset
        /// Universalis上的最后更新时间
        LastUploadTime : DateTimeOffset
        Listings : FableMarketOrder []
        TradeLogs : FableTradeLog []
    }

type MarketInfoCollection private () = 
    inherit CachedItemCollection<string, UniversalisRecord>()

    static let threshold = TimeSpan.FromHours(12.0)

    static let instance = MarketInfoCollection()

    static member Instance = instance

    override x.IsExpired (item) = (DateTimeOffset.Now - item.LastFetchTime) >= threshold

    override x.Depends = Array.empty

    override x.FetchItem(info) =
        let info = MarketInfo.FromString(info)
        let url = sprintf "https://universalis.app/api/%i/%i" info.World.WorldId info.Item.Id
        x.Logger.Info(sprintf "正在访问 %s" url)
        let resp = hc.GetAsync(url)
                     .ConfigureAwait(false)
                     .GetAwaiter()
                     .GetResult()
        if not resp.IsSuccessStatusCode then
            x.Logger.Warn(sprintf "Universalis返回错误%s:%A/%A" resp.ReasonPhrase info.World info.Item)
            match resp.StatusCode with
            | Net.HttpStatusCode.NotFound -> 
                {   Id = info.ToString()
                    LastFetchTime = DateTimeOffset.Now
                    LastUploadTime = DateTimeOffset.Now
                    Listings = Array.empty
                    TradeLogs = Array.empty   }
            | other -> failwithf "Universalis API访问异常，请稍后重试：%O" other
        else
            let json = resp.Content.ReadAsStringAsync()
                        |> Async.AwaitTask
                        |> Async.RunSynchronously
            //x.Logger.Trace(sprintf "universalis.app返回数据：\r\n%s" json)
            let o = JObject.Parse(json)

            let updated = o.["lastUploadTime"].Value<int64>()
                            |> DateTimeOffset.FromUnixTimeMilliseconds
            let listings = 
                [|  let l = o.["listings"] :?> JArray
                    for item in l do 
                        let item = item :?> JObject
                        let odr = LibFFXIV.Network.SpecializedPacket.MarketOrder()
                        odr.TimeStamp <- item.["lastReviewTime"].Value<uint32>()
                        odr.Price <- item.["pricePerUnit"].Value<uint32>()
                        odr.Count <- item.["quantity"].Value<uint32>()
                        odr.IsHQ <- item.["hq"].Value<bool>()
                        yield FableMarketOrder.CreateFrom(info.World.WorldId, odr)  |]

            let tradelogs = 
                [|  let l = o.["recentHistory"] :?> JArray
                    for item in l do 
                        let item = item :?> JObject
                        let log = LibFFXIV.Network.SpecializedPacket.TradeLog()
                        log.IsHQ <- item.["hq"].Value<bool>()
                        log.Price <- item.["pricePerUnit"].Value<uint32>()
                        log.Count <- item.["quantity"].Value<uint32>()
                        log.BuyerName <- item.["buyerName"].Value<string>()
                        log.TimeStamp <- item.["timestamp"].Value<uint32>()
                    
                        yield FableTradeLog.CreateFrom(info.World.WorldId, log)
                |]

            //x.Logger.Info(sprintf "已解析数据:%O %A %A" updated listings tradelogs)

            {   Id = info.ToString()
                LastFetchTime = DateTimeOffset.Now
                LastUploadTime = updated
                Listings = listings
                TradeLogs = tradelogs   }

    member x.GetMarketListings(world : World.World, item : Item.ItemRecord) =
        // universalis.app
        let info = {World = world; Item = item}
        let uniRet = x.[info.ToString()]
        
        // 必须在<@ @>外定义
        let itemId, worldId = item.Id |> uint32, world.WorldId
        let dmfRet =
            MarketOrder.MarketOrderProxy.callSafely <@ fun server -> server.GetByIdWorld worldId itemId @>
            |> Async.RunSynchronously

        match dmfRet with
        | Ok dmf ->
            let getUpdateDate (arr : FableMarketOrder []) = 
                if arr.Length = 0 then
                    DateTimeOffset.MinValue
                else
                    let last = arr |> Array.maxBy (fun l -> l.TimeStamp)
                    DateTimeOffset.FromUnixTimeSeconds(last.TimeStamp |> int64)

            let dmfUpdate = getUpdateDate dmf.Orders
            let uniUpdate = getUpdateDate uniRet.Listings
            //x.Logger.Info(sprintf "DMF更新时间为:%O, UNI更新时间为%O" dmfUpdate uniUpdate)
            if dmfUpdate >= uniUpdate then
                dmf.Orders
            else
                uniRet.Listings
        | Error exn ->
            x.Logger.Error(sprintf "连接DMF异常：%O" exn)
            uniRet.Listings

    member x.GetTradeLogs(world : World.World, item : Item.ItemRecord) =
        // universalis.app
        let info = {World = world; Item = item}
        let uniRet = x.[info.ToString()]

        // 必须在<@ @>外定义
        let itemId, worldId = item.Id |> uint32, world.WorldId
        let dmfRet =
            TradeLog.TradelogProxy.callSafely <@ fun server -> server.GetByIdWorld worldId itemId 20 @>
            |> Async.RunSynchronously

        match dmfRet with
        | Ok dmf ->
            let getUpdateDate (arr : FableTradeLog []) = 
                if arr.Length = 0 then
                    DateTimeOffset.MinValue
                else
                    let last = arr |> Array.maxBy (fun l -> l.TimeStamp)
                    DateTimeOffset.FromUnixTimeSeconds(last.TimeStamp |> int64)

            let dmfUpdate = getUpdateDate dmf
            let uniUpdate = getUpdateDate uniRet.TradeLogs
            //x.Logger.Info(sprintf "DMF更新时间为:%O, UNI更新时间为%O" dmfUpdate uniUpdate)
            if dmfUpdate >= uniUpdate then
                dmf
            else
                uniRet.TradeLogs
        | Error exn ->
            x.Logger.Error(sprintf "连接DMF异常：%O" exn)
            uniRet.TradeLogs