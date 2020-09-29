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

[<CLIMutable>]
type UniversalisRecord = 
    {
        [<BsonId(false)>]
        Id : MarketInfo
        LastUploadTime : DateTimeOffset
        Listings : FableMarketOrder []
        TradeLogs : FableTradeLog []
    }

type MarketInfoCollection private () = 
    inherit CachedItemCollection<MarketInfo, UniversalisRecord>()

    static let threshold = TimeSpan.FromHours(2.0)

    static let instance = MarketInfoCollection()

    static member Instance = instance

    override x.IsExpired (item) = (DateTimeOffset.Now - item.LastUploadTime) >= threshold

    override x.Depends = Array.empty

    override x.FetchItem(info) =
        let url = sprintf "https://universalis.app/api/%i/%i" info.World.WorldId info.Item.Id
        x.Logger.Info(sprintf "正在访问 %s" url)
        let json = hc.GetStringAsync(url)
                     .ConfigureAwait(false)
                     .GetAwaiter()
                     .GetResult()
        x.Logger.Trace(sprintf "universalis.app返回数据：\r\n%s" json)
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

        {   Id = info
            LastUploadTime = updated
            Listings = listings
            TradeLogs = tradelogs   }

    member x.GetMarketListings(world : World.World, item : Item.ItemRecord) =
        // universalis.app
        let info = {World = world; Item = item}
        let uniRet = x.[info]
        
        // 必须在<@ @>外定义
        let itemId, worldId = item.Id |> uint32, world.WorldId
        let dmfRet =
            MarketOrder.MarketOrderProxy.callSafely <@ fun server -> server.GetByIdWorld worldId itemId @>
            |> Async.RunSynchronously

        match dmfRet with
        | Ok dmf ->
            let dmfUpdate = dmf.UpdateTime
            let uniUpdate = 
                let last = uniRet.Listings |> Array.maxBy (fun l -> l.TimeStamp)
                DateTimeOffset.FromUnixTimeSeconds(last.TimeStamp |> int64)
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
        let uniRet = x.[info]

        // 必须在<@ @>外定义
        let itemId, worldId = item.Id |> uint32, world.WorldId
        let dmfRet =
            TradeLog.TradelogProxy.callSafely <@ fun server -> server.GetByIdWorld worldId itemId 20 @>
            |> Async.RunSynchronously

        match dmfRet with
        | Ok dmf ->
            let dmfUpdate = 
                let last = dmf |> Array.maxBy (fun l -> l.TimeStamp)
                DateTimeOffset.FromUnixTimeSeconds(last.TimeStamp |> int64)
            let uniUpdate = 
                let last = uniRet.Listings |> Array.maxBy (fun l -> l.TimeStamp)
                DateTimeOffset.FromUnixTimeSeconds(last.TimeStamp |> int64)

            if dmfUpdate >= uniUpdate then
                dmf
            else
                uniRet.TradeLogs
        | Error exn ->
            x.Logger.Error(sprintf "连接DMF异常：%O" exn)
            uniRet.TradeLogs