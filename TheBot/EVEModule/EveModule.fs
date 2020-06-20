﻿namespace TheBot.Module.EveModule
open System
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Handler.CommandHandlerBase
open TheBot.Utils.HandlerUtils
open TheBot.Utils.TextTable
open TheBot.Utils.RecipeRPN
open TheBot.Module.EveModule.Utils
open EveData
open TheBot.Module.EveModule.Extensions

type EveModule() =
    inherit CommandHandlerBase()

    let typeNameToItem(name : string) =
        let succ, item = EveTypeNameCache.TryGetValue(name)
        if not succ then
            failwithf "找不到物品"
        item

    let tryTypeToBp(item : EveType) = 
        let succ, bp = EveBlueprintCache.TryGetValue(item.TypeId)
        if not succ then
            let succ, bp = itemToBp.TryGetValue(item.TypeId)
            if not succ then
                None
            else
                // 输入是物品，但是存在制造蓝图
                Some(bp)
        else
            // 输入是蓝图
            Some(bp)

    let typeToBp(item : EveType) = 
        let ret = tryTypeToBp(item)
        if ret.IsNone then failwithf "找不到蓝图信息: %s" item.TypeName 
        ret.Value

    /// 物品名或蓝图名查找蓝图
    let typeNameToBp(name : string) = 
        name |> typeNameToItem |> typeToBp

    static let er = EveExpression.EveExpression()

    [<CommandHandlerMethodAttribute("eveTest", "测试用（管理员）", "")>]
    member x.HandleEveTest(msgArg : CommandArgs) =
        failOnNonAdmin(msgArg)

        let (name, cfg) = ScanConfig(msgArg.Arguments)
        let succ, crop = CorporationName.TryGetValue(name)
        if not succ then
            failwithf "找不到NPC信息：%s" name
        let tt = TextTable.FromHeader([|"兑换"; "成本"; "收益/LP"; |])
        tt.AddPreTable("暂不支持蓝图计算，蓝图兑换已删除。")
        GetLpStoreOffersByCorp(crop)
        |> Array.filter (fun offer -> not <| EveBlueprintCache.ContainsKey(offer.Offer.TypeId))
        |> Array.map (fun offer ->
            let item = EveTypeIdCache.[offer.Offer.TypeId]
            let itemCost = 
                offer.Required
                |> Array.sumBy (fun m -> m.GetTotalPrice())

            let totalCost = itemCost + offer.IskCost
            let profit = 
                let sell = offer.Offer.GetTotalPrice()
                sell - totalCost
            let profitPerLp = profit / offer.LpCost
            let offerStr = sprintf "%s*%g" item.TypeName offer.Offer.Quantity

            (offerStr, totalCost, profitPerLp))
        |> Array.sortByDescending (fun (_, _, ppl) -> ppl)
        //|> Array.truncate 20
        |> Array.iter (fun (n, c, ppl) -> tt.AddRow(n, c, ppl))

        use tr = new TextResponse(msgArg)
        tr.Write(tt)

    [<CommandHandlerMethodAttribute("updateevedb", "刷新价格数据库（管理员）", "")>]
    member x.HandleRefreshCache(msgArg : CommandArgs) =
        failOnNonAdmin(msgArg)
        failwithf "此命令暂时废弃"
        let updated = UpdatePriceCache()
        msgArg.QuickMessageReply(sprintf "价格缓存刷新成功 @ %O" updated)

    [<CommandHandlerMethodAttribute("em", "查询物品价格", "")>]
    [<CommandHandlerMethodAttribute("emr", "实时价格查询（市场中心API）", "")>]
    member x.HandleEveMarket(msgArg : CommandArgs) =
        let t = er.Eval(String.Join(" ", msgArg.Arguments))
        let priceFunc = 
            match msgArg.Command with
            | Some "#em"  -> fun (t : EveType) -> t.GetPrice()
            | Some "#emr" -> fun (t : EveType) -> GetItemPrice(t.TypeId)
            | _ -> failwithf "%A" msgArg.Command

        let tt = TextTable.FromHeader([|"物品"; "数量"; "卖出"; "买入"; "更新时间"|])

        match t with
        | Accumulator a ->
            for kv in a do 
                let item = kv.Key
                let q    = kv.Value
                let p    = priceFunc(item)
                let sell = p.Sell * q
                let buy  = p.Buy * q
                let updated = p.Updated.ToOffset(TimeSpan.FromHours(8.0))
                tt.AddRow(item.TypeName, q, sell, buy, updated)
                ()
        | _ -> failwithf "求值失败，结果是%A" t

        msgArg.QuickMessageReply(tt.ToString())

    
    member x.HandleEveMarketR(msgArg : CommandArgs) =
        let name = String.Join(" ", msgArg.Arguments)
        let item = typeNameToItem(name)
        let sell, buy = item.GetSellPrice(), item.GetBuyPrice()
        msgArg.QuickMessageReply(String.Format("{0} 出售：{1:N2} 买入：{2:N2}", item.TypeName, sell, buy))

    [<CommandHandlerMethodAttribute("eme", "EVE蓝图材料效率计算", "")>]
    member x.HandleME(msgArg : CommandArgs) =
        let name = String.Join(" ", msgArg.Arguments)
        let bp = typeNameToBp(name)
        let me0Price = bp.ApplyMaterialEfficiency(0).GetTotalMaterialPrice()

        let att = AutoTextTable<int>(
                    [|
                        "材料等级", fun me -> box(me)
                        "节省", fun me -> 
                                        bp.ApplyMaterialEfficiency(me).GetTotalMaterialPrice()
                                        |> (fun x -> (me0Price - x))
                                        |> box
                    |]
        )
        att.AddPreTable(sprintf "产出 %s" EveTypeIdCache.[bp.ProductId].TypeName)
        att.AddPreTable("直接材料总价：" + System.String.Format("{0:N0}", me0Price))
        for i = 0 to 10 do
            att.AddObject(i)
        msgArg.QuickMessageReply(att.ToString())

    [<CommandHandlerMethodAttribute("er", "EVE蓝图材料计算（可用表达式）", "")>]
    member x.HandleR(msgArg : CommandArgs) =
        let (expr, cfg) = ScanConfig(msgArg.Arguments)

        let final = ItemAccumulator()
        let tt = TextTable.FromHeader([|"名称"; "数量";|])
        tt.AddPreTable(sprintf "输入效率：%i%% "cfg.InputME)
        match er.Eval(expr) with
        | Number n ->
            failwithf "结算结果为数字:%g" n
        | Accumulator a ->
            for kv in a do 
                let q  = kv.Value

                let bp = tryTypeToBp(kv.Key)
                
                match bp with
                | _ when bp.IsSome && q > 0.0 -> 
                    // 需要计算
                    let bp = typeToBp(kv.Key).ApplyMaterialEfficiency(cfg.InputME).GetBpByRuns(q)
                    tt.AddRow("产出："+bp.ProductItem.TypeName, bp.ProductQuantity)

                    for m in bp.Materials do
                        final.AddOrUpdate(m.MaterialItem, m.Quantity)

                | _ when q < 0.0 -> 
                    // 已有材料需要扣除
                    final.AddOrUpdate(kv.Key, kv.Value)
                | _ -> failwithf "不知道如何处理：%s * %g" kv.Key.TypeName kv.Value

            
        for kv in final do 
            tt.AddRow(kv.Key.TypeName, kv.Value)
        msgArg.QuickMessageReply(tt.ToString())

    [<CommandHandlerMethodAttribute("err", "EVE蓝图材料计算（可用表达式）", "")>]
    member x.HandleRR(msgArg : CommandArgs) =
        let (expr, cfg) = ScanConfig(msgArg.Arguments)

        let final = ItemAccumulator()
        let tt = TextTable.FromHeader([|"名称"; "数量";|])
        tt.AddPreTable(sprintf "输入效率：%i%% 默认效率：%i%%"
            cfg.InputME
            cfg.DefME
        )
        tt.AddPreTable(sprintf "展开行星材料：%b 展开反应公式：%b" cfg.ExpandPlanet cfg.ExpandReaction)

        let rec loop (bp : EveData.EveBlueprint) = 
            for m in bp.Materials do 
                let hasNext, bp = itemToBp.TryGetValue(m.TypeId)
                if hasNext && cfg.BpCanExpand(bp) then
                    let bp = bp.ApplyMaterialEfficiency(cfg.DefME).GetBpByItemCeil(m.Quantity)
                    loop(bp)
                else
                    final.AddOrUpdate(m.MaterialItem, m.Quantity)

        match er.Eval(expr) with
        | Number n ->
            failwithf "结算结果为数字:%g" n
        | Accumulator a ->
            for kv in a do 
                let q  = kv.Value
                let bp = typeToBp(kv.Key).ApplyMaterialEfficiency(cfg.InputME).GetBpByRuns(q)
                tt.AddRow("产出："+ bp.ProductItem.TypeName, bp.ProductQuantity)

                loop(bp)
            
        for kv in final do 
            tt.AddRow(kv.Key.TypeName, kv.Value)
        msgArg.QuickMessageReply(tt.ToString())

    [<CommandHandlerMethodAttribute("errc", "EVE蓝图成本计算（可用表达式）", "")>]
    member x.HandleERRCV2(msgArg : CommandArgs) = 
        let (expr, cfg) = ScanConfig(msgArg.Arguments)

        let finalBp = 
            match er.Eval(expr) with
            | Number n ->
                failwithf "结算结果为数字:%g" n
            | Accumulator a ->
                /// 生成一个伪蓝图用于下游计算
                let os = ItemAccumulator<EveType>()
                let ms = ItemAccumulator<EveType>()
                for kv in a do
                    let t = kv.Key
                    let q = kv.Value
                    if q < 0.0 then failwith "暂不支持负数计算"

                    let bp = (typeToBp t).GetBpByRuns(q)

                    os.AddOrUpdate(bp.ProductItem, bp.ProductQuantity)
                    for m in bp.Materials do
                        ms.AddOrUpdate(m.MaterialItem, m.Quantity)

                let ms = 
                    [| for kv in ms do yield {EveMaterial.TypeId = kv.Key.TypeId; EveMaterial.Quantity = kv.Value} |]
                let os = 
                    [| for kv in os do yield {EveMaterial.TypeId = kv.Key.TypeId; EveMaterial.Quantity = kv.Value} |]

                {   EveBlueprint.Materials = ms
                    EveBlueprint.Products = os
                    EveBlueprint.BlueprintTypeID = Int32.MinValue
                    EveBlueprint.Type = BlueprintType.Unknown}

        let outTt = TextTable.FromHeader([|"产出"; "数量";|])
        for p in finalBp.Products do 
            outTt.AddRow(p.MaterialItem.TypeName, p.Quantity)

        let tt = TextTable.FromHeader([|"组件"; "数量"; "买成品"; "搓（材料+费）"|])
        //tt.AddPreTable("计算结果")
        //tt.AddPreTable(outTt.ToString())
        tt.AddPreTable("价格有延迟，算法不稳定，市场有风险, 投资需谨慎")
        tt.AddPreTable(sprintf "输入效率：%i%% 默认效率：%i%% 成本指数：%i%% 设施税率%i%%"
            cfg.InputME
            cfg.DefME
            cfg.SystemCostIndex
            cfg.StructureTax
        )
        tt.AddPreTable(sprintf "展开行星材料：%b 展开反应公式：%b" cfg.ExpandPlanet cfg.ExpandReaction)

        

        let getFee price = 
            let fee = price * (pct cfg.SystemCostIndex)
            let tax = fee * (pct cfg.StructureTax)
            fee + tax

        let rec getRootMaterialsPrice (bp : EveBlueprint) = 
            let mutable sum = 0.0

            for m in bp.Materials do 
                let price = m.GetTotalPrice()
                let fee = getFee(price)
                sum <- sum + fee

                let hasNext, bp = itemToBp.TryGetValue(m.TypeId)
                if hasNext && cfg.BpCanExpand(bp) then
                    let bp = bp.ApplyMaterialEfficiency(cfg.DefME).GetBpByItemNoCeil(m.Quantity)
                    sum <- sum + getRootMaterialsPrice bp
                else
                    sum <- sum + price
            sum

        let mutable optCost = 0.0
        let mutable allCost = 0.0

        // 所有材料
        // 材料名称 数量 售价（小计） 制造成本（小计） 最佳成本（小计）
        for m in finalBp.Materials do 
            let amount = m.Quantity
            let name   = m.MaterialItem.TypeName
            let buy    = m.GetTotalPrice()
                
            optCost <- optCost + getFee(buy)
            allCost <- allCost + getFee(buy)

            let succ, bp = itemToBp.TryGetValue(m.TypeId)
            if succ && cfg.BpCanExpand(bp) then
                let bp = bp.ApplyMaterialEfficiency(cfg.DefME).GetBpByItemNoCeil(amount)
                let cost = getRootMaterialsPrice bp
                optCost <- optCost + (if (cost >= buy) && (buy <> 0.0) then buy else cost)
                allCost <- allCost + cost
                tt.AddRow(name, amount, buy |> HumanReadableFloat , cost |> HumanReadableFloat)
            else
                optCost <- optCost + buy
                allCost <- allCost + buy
                tt.AddRow(name, amount, buy |> HumanReadableFloat, "--")

        let sell = finalBp.GetTotalProductPrice()

        tt.AddRowPadding("售价/税后", "--", sell |> HumanReadableFloat, sell * 0.94 |> HumanReadableFloat)
        tt.AddRowPadding("买入造价/最佳造价", "--", optCost |> HumanReadableFloat, allCost |> HumanReadableFloat)
        msgArg.QuickMessageReply(tt.ToString())

    [<CommandHandlerMethodAttribute("EVE压矿", "EVE压矿利润", "")>]
    member x.HandleOreCompression(msgArg : CommandArgs) =
        let ores = 
            EveTypeNameCache.Values
            |> Seq.filter (fun x -> x.TypeName.StartsWith("高密度"))
        let tt = TextTable.FromHeader([|"矿"; "压缩前（1化矿单位）"; "压缩后"; "溢价比"|])
        for ore in ores do
            if not <| ore.TypeName.Contains("冰") then
                let compressed = ore
                let normal     = EveTypeNameCache.[ore.TypeName.[3..]]

                let cp = GetItemPriceCached(compressed.TypeId).Sell
                let np = GetItemPriceCached(normal.TypeId).Sell

                if np <> 0.0 then
                    tt.AddRow(normal.TypeName, np*100.0, cp, cp/np/100.0)
        msgArg.QuickMessageReply(tt.ToString())

    [<CommandHandlerMethodAttribute("EVE采矿", "EVE挖矿利润", "")>]
    [<CommandHandlerMethodAttribute("EVE挖矿", "EVE挖矿利润", "")>]
    member x.HandleOreMining(msgArg : CommandArgs) =
        let mineSpeed = 10.0 // m^3/s
        let refineYield = 0.70

        let tt = TextTable.FromHeader([|"矿石"; "秒利润"; "冰矿"; "秒利润"; "月矿"; "秒利润";|])
        tt.AddPreTable(sprintf "采集能力：%g m3/s 精炼效率:%g"
            mineSpeed
            refineYield
        )
            
        let getSubTypes (names : string) = 
            names.Split(',')
            |> Array.map (fun name ->
                let info = OreRefineInfo.[name]
                let refinePerSec = mineSpeed / info.Volume / info.RefineUnit
                let price =
                    info.Yields
                    |> Array.sumBy (fun m -> 
                        m.Quantity * refinePerSec * refineYield * GetItemPriceCached(m.TypeId).Sell)
                name, price|> ceil )
            |> Array.sortByDescending snd

        let moon= getSubTypes EveData.MoonNames
        let ice = getSubTypes EveData.IceNames
        let ore = getSubTypes EveData.OreNames

        let tryGetRow (arr : (string * float) [])  (id : int)  = 
            if id <= arr.Length - 1 then
                let n,p = arr.[id]
                (box n, box p)
            else
                (box "--", box "--")

        let rowMax = (max (max ice.Length moon.Length) ore.Length) - 1
        for i = 0 to rowMax do 
            let eon, eop = tryGetRow ore i
            let ein, eip = tryGetRow ice i
            let emn, emp = tryGetRow moon i
            tt.AddRow(eon, eop, ein, eip, emn, emp)

        msgArg.QuickMessageReply(tt.ToString())