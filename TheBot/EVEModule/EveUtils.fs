﻿module TheBot.Module.EveModule.Utils
open System
open System.Xml
open System.Collections.Generic
open System.Net.Http
open Newtonsoft.Json.Linq

type internal PriceCache = 
    {
        TypeId : int
        Price : float
        Updated : DateTime
    }

    member x.NeedsUpdate() = 
        (DateTime.Now - x.Updated) >= PriceCache.Threshold

    static member Threshold = TimeSpan.FromHours(24.0)

let private MarketEndpoint = sprintf @"https://www.ceve-market.org/api/market/region/10000002/system/30000142/type/%i.json"
let private hc = new HttpClient()

let private globalCache = Dictionary<int, PriceCache>()

let GetItemPrice (typeid : int) = 
    let url = MarketEndpoint typeid
    printfn "正在请求：%s" url
    let json = hc
                .GetStringAsync(url)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()
    let obj = JObject.Parse(json)
    let obj = obj.GetValue("sell") :?> JObject
    obj.GetValue("min").ToObject<float>()

let GetItemPriceCached (item : int) =
    let succ, price = globalCache.TryGetValue(item)
    if (not succ) || price.NeedsUpdate() then
        let cache = 
            {
                TypeId = item
                Price  = GetItemPrice(item)
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
        MaterialEfficiency : int
        SystemCostIndex    : int
        StructureBonuses   : int
        StructureTax       : int
        InitRuns           : int
        InitItems          : int
    }

    member x.GetRuns(bp : EveData.EveBlueprint) = 
        let runs = x.InitRuns |> float
        let items = x.InitItems |> float
        if runs <> 0.0 then
            runs
        else
            if items = 0.0 then
                invalidOp ""
            else
                items / (bp.Products |> Array.head).Quantity

    static member Default = 
        {
            MaterialEfficiency = 5
            SystemCostIndex    = 5
            StructureBonuses   = 100
            StructureTax       = 10
            InitRuns           = 0
            InitItems          = 1
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
                    if not vsucc then
                        failwithf "参数不合法"
                    match arg.ToLowerInvariant() with
                    |  "me" -> config <- {config with MaterialEfficiency = value}
                    | "sci" -> config <- {config with SystemCostIndex = value}
                    |  "sb" -> config <- {config with StructureBonuses = value}
                    | "tax" -> config <- {config with StructureTax = value}
                    | "run" -> config <- {config with InitRuns = value}
                    |"item" -> config <- {config with InitItems = value}
                    | _ -> ()
                else
                    yield i
        |]
    String.Join(" ", left), config