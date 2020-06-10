module TheBot.Module.EveModule.Utils
open System
open System.Xml
open System.Collections.Generic
open System.Net.Http
open Newtonsoft.Json.Linq
open EveData

let (EveTypeIdCache, EveTypeNameCache) = 
    let name = new Dictionary<string, EveType>()
    let id   = new Dictionary<int, EveType>()

    for item in EveData.EveType.GetEveTypes() do 
        if not <| name.ContainsKey(item.TypeName) then
            name.Add(item.TypeName, item)
        id.Add(item.TypeId, item)

    (id, name)

let (EveBlueprintCache, itemToBp) = 
    let bp = Dictionary<int, EveBlueprint>()
    let item = Dictionary<int, EveBlueprint>()

    for bpinfo in EveBlueprint.GetBlueprints() do 
        bp.Add(bpinfo.BlueprintTypeID, bpinfo)
        let plen = bpinfo.Products.Length
        if plen = 1 then
            let isManufacturing = bpinfo.Type = BlueprintType.Manufacturing
            let doAddCache = 
                match bpinfo.Type with
                | BlueprintType.Planet -> true
                | BlueprintType.Reaction
                    when EveTypeIdCache.ContainsKey(bpinfo.BlueprintTypeID) -> true
                | BlueprintType.Manufacturing
                    when EveTypeIdCache.ContainsKey(bpinfo.BlueprintTypeID) -> true
                | _ -> false

            if doAddCache then
                item.Add(bpinfo.ProductId, bpinfo)
        elif plen = 0 then
            printfn "Bp ignored : No products : %A" bpinfo
        else
            printfn "Bp ignored : Multiple products : %A" bpinfo

    (bp, item)

let CorporationName = 
    NpcCorporation.GetNpcCorporations()
    |> Seq.map (fun i -> i.CorporationName, i)
    |> readOnlyDict

/// 采矿分析数据
let OreRefineInfo = 
    seq {
        let moon = MoonNames.Split(',') |> set
        let ice  = IceNames.Split(',') |> set
        let ore  = OreNames.Split(',') |> set

        for tid, ms in RefineInfo.GetRefineInfos() do 
            let succ, t = EveTypeIdCache.TryGetValue(tid)
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
                    yield tn, refine
    }
    |> readOnlyDict

type internal PriceCache = 
    {
        TypeId : int
        Price : float
        Updated : DateTime
    }

    member x.NeedsUpdate() = 
        (DateTime.Now - x.Updated) >= PriceCache.Threshold

    static member Threshold = TimeSpan.FromHours(24.0)

let private hc = new HttpClient()

let private globalCache = Dictionary<int, PriceCache>()

let GetItemPrice (typeid : int) = 
    let url = sprintf @"https://www.ceve-market.org/api/market/region/10000002/system/30000142/type/%i.json" typeid
    printfn "正在请求：%s" url
    let json = hc
                .GetStringAsync(url)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()
    let obj = JObject.Parse(json)
    let sellMin = (obj.GetValue("sell") :?> JObject).GetValue("min").ToObject<float>()
    let buyMax  = (obj.GetValue("buy") :?> JObject).GetValue("max").ToObject<float>()

    sellMin, buyMax

let GetItemPriceCached (item : int) =
    let succ, price = globalCache.TryGetValue(item)
    if (not succ) || price.NeedsUpdate() then
        let cache = 
            {
                TypeId = item
                Price  = GetItemPrice(item) |> fst
                Updated = DateTime.Now
            }
        globalCache.[item] <- cache
    globalCache.[item].Price

let UpdatePriceCache() = 
    let url = "https://www.ceve-market.org/dumps/price_all.xml"
    let ret = hc.GetAsync(url)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()
    let date = 
        ret.Content.Headers.LastModified
        |> Option.ofNullable
        |> Option.map (fun dt -> dt.LocalDateTime)

    let updated = 
        date
        |> Option.defaultValue DateTime.Now

    let str = 
        // 避免中途中断，一次读完
        ret.Content.ReadAsStringAsync()
           .ConfigureAwait(false)
           .GetAwaiter()
           .GetResult()
    let xml = XmlDocument()
    xml.LoadXml(str)
    for node in xml.GetElementsByTagName("row") do 
        let attrs = node.Attributes
        let tid   = attrs.["typeID"].Value |> int32
        let price = attrs.["lo"].Value |> float
        globalCache.[tid] <- {TypeId = tid; Price = price; Updated = updated}

    updated

type FinalMaterials() =
    let m = Dictionary<EveData.EveType, float>()

    member x.AddOrUpdate(item, runs) =
        if m.ContainsKey(item) then m.[item] <- m.[item] + runs
        else m.Add(item, runs)

    member x.Get() =
        [| for kv in m do
            yield (kv.Key, kv.Value) |]

type EveCalculatorConfig = 
    {
        // 默认材料效率
        DefME : int
        // 输入物品材料效率
        InputME : int

        SystemCostIndex    : int
        StructureTax       : int
        InitRuns           : int
        ExpandReaction     : bool
        ExpandPlanet       : bool
    }

    /// 测试蓝图能否继续展开
    member x.BpCanExpand(bp : EveData.EveBlueprint) = 
        match bp.Type with
        | BlueprintType.Manufacturing -> true
        | BlueprintType.Planet -> x.ExpandPlanet
        | BlueprintType.Reaction -> x.ExpandReaction
        | _ -> failwithf "未知蓝图类型 %A" bp

    /// 调整蓝图信息，合并计算材料效率和流程/物品数
    member x.ConfigureBlueprint(bp : EveData.EveBlueprint) = 
        let runs = x.InitRuns |> float
        let bp = bp.ApplyMaterialEfficiency(x.InputME)
        
        if runs > 0.0 then
            bp.GetBpByRuns(runs)
        else
            invalidOp "流程数无效"

    static member Default = 
        {
            // 默认材料效率
            DefME = 10
            // 输入图材料效率
            InputME = 10
            SystemCostIndex    = 5
            StructureTax       = 10
            InitRuns           = 1
            ExpandReaction     = false
            ExpandPlanet       = false
        }

/// 扫描输入参数，返回物品名和参数
// 如果发现"xxx:yyy"格式，视为参数
let ScanConfig (input : string[]) = 
    let mutable config = EveCalculatorConfig.Default
    let left = 
        [|
            for i in input do 
                if i.Contains(":") || i.Contains("：") then
                    let s = i.Split(':', '：')
                    let arg, (vsucc, value) = s.[0], Int32.TryParse(s.[1])
                    let value = 
                        if vsucc then
                            value
                        else
                            0
                    match arg.ToLowerInvariant() with
                    | "ime" -> config <- {config with InputME = value}
                    | "dme" -> config <- {config with DefME = value}
                    |  "me" -> config <- {config with InputME = value; DefME = value}
                    | "sci" -> config <- {config with SystemCostIndex = value}
                    | "tax" -> config <- {config with StructureTax = value}
                    | "run" -> config <- {config with InitRuns = value}
                    |   "p" -> config <- {config with ExpandPlanet = true}
                    | "日球" -> config <- {config with ExpandPlanet = true}
                    |   "r" -> config <- {config with ExpandReaction = true}
                    | "反应" -> config <- {config with ExpandReaction = true}
                    | _ -> ()
                else
                    yield i
        |]
    
    String.Join(" ", left), config

let GetLpStoreOffersByCorp(corp : NpcCorporation) = 
    let url =
        sprintf "https://esi.evepc.163.com/latest/loyalty/stores/%i/offers/?datasource=serenity"
            corp.CorporationID
    printfn "正在请求：%s" url
    let json = hc
                .GetStringAsync(url)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()
    [|
        for item in JArray.Parse(json) do 
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
                    [|
                        for ii in item.GetValue("required_items") :?> JArray do 
                            let i = ii :?> JObject
                            let iq = i.GetValue("quantity").ToObject<float>()
                            let it = i.GetValue("type_id").ToObject<int>()
                            yield {EveMaterial.TypeId = it; Quantity = iq}
                    |]

                yield {
                    IskCost = isk
                    LpCost  = lp
                    OfferId = id
                    Offer = offers
                    Required = requires
                }
    |]