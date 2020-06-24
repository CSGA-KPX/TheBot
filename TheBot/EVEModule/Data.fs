namespace TheBot.Module.EveModule.Utils.Data

open System
open System.Collections.Generic

open Newtonsoft.Json.Linq

open EveData

open TheBot.Module.EveModule.Utils.Helpers

type PriceCache = 
    {
        TypeId : int
        Sell   : float
        Buy    : float
        Updated : DateTimeOffset
    }

type PriceCacheCollection() = 
    inherit CachedCollection<int, PriceCache>()

    static let threshold = TimeSpan.FromHours(6.0)

    override x.GetKey(item) = item.TypeId

    override x.IsExpired(item) =
        (DateTimeOffset.Now - item.Updated) >= threshold

    override x.FetchItem(key) = 
        let url = sprintf @"https://www.ceve-market.org/api/market/region/10000002/system/30000142/type/%i.json" key

        let json = hc
                    .GetStringAsync(url)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult()

        let obj = JObject.Parse(json)
        let sellMin = (obj.GetValue("sell") :?> JObject).GetValue("min").ToObject<float>()
        let buyMax  = (obj.GetValue("buy") :?> JObject).GetValue("max").ToObject<float>()

        {   TypeId  = key
            Sell    = sellMin
            Buy     = buyMax
            Updated = DateTimeOffset.Now }

type MarketHistoryRecord = 
    {
        Average : float
        Date    : DateTimeOffset
        Highest : float
        Lowest  : float
        OrderCount: int
        Volume  : int64
    }

type TradeVolumeCacheCollection() = 
    inherit CachedCollection<int, int * float>()

    static let threshold = TimeSpan.FromHours(24.0)

    override x.GetKey(item) = fst item

    override x.IsExpired(item) = false

    override x.FetchItem(key) = 
        let url =
            sprintf "https://esi.evepc.163.com/latest/markets/10000002/history/?datasource=serenity&type_id=%i" key
        let json = hc
                    .GetStringAsync(url)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult()

        let history = JArray.Parse(json).ToObject<MarketHistoryRecord[]>()
        if history.Length = 0 then key, 0.0
        else key, history |> Array.averageBy (fun x -> x.Volume |> float)

type LpStoreOffersCacheCollection() = 
    inherit CachedCollection<int, int * (LoyaltyStoreOffer [])>()

    static let threshold = TimeSpan.FromHours(24.0)

    override x.GetKey(item) = fst item

    override x.IsExpired(item) = false

    override x.FetchItem(key) = 
            let url =
                sprintf "https://esi.evepc.163.com/latest/loyalty/stores/%i/offers/?datasource=serenity" key
            let json = hc
                        .GetStringAsync(url)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult()
            key, [|  for item in JArray.Parse(json) do 
                        let item = item :?> JObject
                        // 无视所有分析点兑换
                        if not <| item.ContainsKey("ak_cost") then
                            let isk = item.GetValue("isk_cost").ToObject<float>()
                            let lp  = item.GetValue("lp_cost").ToObject<float>()
                            let id  = item.GetValue("offer_id").ToObject<int>()
                            let offers = 
                                let q   = item.GetValue("quantity").ToObject<float>()
                                let t   = item.GetValue("type_id").ToObject<int>()
                                {EveMaterial.TypeId = t; Quantity = q}

                            let requires = 
                                [| for ii in item.GetValue("required_items") :?> JArray do 
                                       let i = ii :?> JObject
                                       let iq = i.GetValue("quantity").ToObject<float>()
                                       let it = i.GetValue("type_id").ToObject<int>()
                                       yield {EveMaterial.TypeId = it; Quantity = iq} |]

                            yield { IskCost = isk
                                    LpCost  = lp
                                    OfferId = id
                                    Offer = offers
                                    Required = requires } |]

type DataBundle private () = 
    let logger = NLog.LogManager.GetCurrentClassLogger()
    
    let typeName = Dictionary<string, EveType>()
    let typeId   = Dictionary<int, EveType>()

    let bpIdToBp = Dictionary<int, EveBlueprint>()
    let productToBp = Dictionary<int, EveBlueprint>()

    let npcCorpNames = Dictionary<string, NpcCorporation>()

    let refineInfo = Dictionary<int, RefineInfo>()

    let priceCache = PriceCacheCollection()
    let volumeCache = TradeVolumeCacheCollection()
    let lpStoreCache = LpStoreOffersCacheCollection()

    let itemNotFound = failwithf "找不到物品 %s"

    member private x.InitTypes() = 
        for item in EveData.EveType.GetEveTypes() do 
            if typeName.ContainsKey(item.TypeName) then
                logger.Fatal("跳过 物品名重复：{0}", sprintf "%A" item)
            else
                typeName.Add(item.TypeName, item)
            typeId.Add(item.TypeId, item)

    member private x.InitBlueprints() = 
        for bpinfo in EveBlueprint.GetBlueprints() do 
            bpIdToBp.Add(bpinfo.BlueprintTypeID, bpinfo)
            let plen = bpinfo.Products.Length
            if plen = 1 then
                let isManufacturing = bpinfo.Type = BlueprintType.Manufacturing
                let doAddCache = 
                    match bpinfo.Type with
                    | BlueprintType.Planet -> true
                    | BlueprintType.Reaction
                        when typeId.ContainsKey(bpinfo.BlueprintTypeID) -> true
                    | BlueprintType.Manufacturing
                        when typeId.ContainsKey(bpinfo.BlueprintTypeID) -> true
                    | _ -> false

                if doAddCache then
                    productToBp.Add(bpinfo.ProductId, bpinfo)
            elif plen = 0 then
                logger.Fatal("跳过 无产品 ：{0}", sprintf "%A" bpinfo)
            else
                logger.Fatal("跳过 多个产品 ：{0}", sprintf "%A" bpinfo)

    member private x.InitNpcCorp() = 
        for c in NpcCorporation.GetNpcCorporations() do
            npcCorpNames.Add(c.CorporationName, c)
    
    member private x.InitRefineInfo() = 
        let moon = MoonNames.Split(',') |> set
        let ice  = IceNames.Split(',') |> set
        let ore  = OreNames.Split(',') |> set

        for tid, ms in RefineInfo.GetRefineInfos() do 
            let succ, t = typeId.TryGetValue(tid)
            // 25 = 小行星
            if succ && t.CategoryId = 25 then
                let tn = t.TypeName
                let isMoon = moon.Contains(tn)
                let isIce  = ice.Contains(tn)
                let isOre  = ore.Contains(tn)
                if isMoon || isIce || isOre then
                    let refine = 
                        {
                            OreType = t
                            Volume = t.Volume
                            RefineUnit = 
                                if isMoon || isOre then 100.0
                                elif isIce then 1.0
                                else failwith "这不是矿"
                            Yields = ms
                        }
                    refineInfo.Add(t.TypeId, refine)

    member private x.InitData() = 
        // 注意生成顺序
        x.InitTypes()
        x.InitBlueprints()
        x.InitNpcCorp()
        x.InitRefineInfo()

    member x.ClearPriceCache() = priceCache.Clear()

    member x.TryGetItem(str : string) = 
        if typeName.ContainsKey(str) then
            Some typeName.[str]
        else
            None 

    member x.GetItem(str : string) = 
        if typeName.ContainsKey(str) then
            typeName.[str]
        else
            itemNotFound str

    member x.GetItem(id : int) = typeId.[id]

    member x.TryTypeToBp(t : EveType) = 
        let succ, bp = bpIdToBp.TryGetValue(t.TypeId)
        if not succ then
            x.TryGetBpByProduct(t)
        else
            // 输入是蓝图
            Some(bp)

    member x.TryGetBp(id : int) = 
        let succ, bp = bpIdToBp.TryGetValue(id)
        if succ then Some bp else None

    member x.GetBp(id : int) = bpIdToBp.[id]

    member x.GetBpByProduct(id : int) = productToBp.[id]

    member x.GetBpByProduct(t : EveType) = x.GetBpByProduct(t.TypeId)

    member x.TryGetBpByProduct(t : EveType) = 
        let succ, bp = productToBp.TryGetValue(t.TypeId)
        if succ then Some bp else None

    member x.GetItemPrice(t : EveType)  = x.GetItemPrice(t.TypeId)
    member x.GetItemPrice(id : int) = priceCache.FetchItem(id)
    member x.GetItemPriceCached(t : EveType) = x.GetItemPriceCached(t.TypeId)
    member x.GetItemPriceCached(id : int) = priceCache.[id]

    member x.GetNpcCorporation(name : string) = npcCorpNames.[name]

    member x.GetItemTradeVolume(t : EveType) = volumeCache.[t.TypeId] |> snd

    member x.GetLpStoreOffersByCorp(c : NpcCorporation) = lpStoreCache.[c.CorporationID] |> snd

    member x.GetRefineInfo(t : EveType) = refineInfo.[t.TypeId]

    static member val Instance = 
        let i = DataBundle()
        i.InitData()
        i
