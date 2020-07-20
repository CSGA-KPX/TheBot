namespace TheBot.Module.EveModule

open System

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Handler.CommandHandlerBase

open BotData.EveData.Utils
open BotData.EveData.EveType
open BotData.EveData.EveBlueprint

open TheBot.Utils.HandlerUtils
open TheBot.Utils.TextTable
open TheBot.Utils.RecipeRPN

open TheBot.Module.EveModule.Utils.Helpers
open TheBot.Module.EveModule.Utils.Config
open TheBot.Module.EveModule.Utils.Data
open TheBot.Module.EveModule.Utils.Extensions

type EveModule() =
    inherit CommandHandlerBase()

    let data = DataBundle.Instance

    let tryTypeToBp(item : EveType) = data.TryTypeToBp(item)

    let typeToBp(item : EveType) = 
        let ret = tryTypeToBp(item)
        if ret.IsNone then failwithf "找不到蓝图信息: %s" item.Name 
        ret.Value

    /// 物品名或蓝图名查找蓝图
    let typeNameToBp(name : string) = 
        name |> data.GetItem |> typeToBp

    let ToolWarning = "价格有延迟，算法不稳定，市场有风险, 投资需谨慎"
    let er = EveExpression.EveExpression()

    [<CommandHandlerMethodAttribute("eveTest", "测试用（管理员）", "")>]
    member x.HandleEveTest(msgArg : CommandArgs) =
        failOnNonAdmin(msgArg)
        let cfg = EveConfigParser()
        cfg.Parse(msgArg.Arguments)

    [<CommandHandlerMethodAttribute("eveLp", "EVE LP兑换计算", "#evelp 军团名")>]
    member x.HandleEveLp(msgArg : CommandArgs) =
        let cfg = Utils.LpUtils.LpConfigParser()
        cfg.Parse(msgArg.Arguments)

        let tt = TextTable.FromHeader([|"兑换"; RightAlignCell "利润"; RightAlignCell "利润/LP"; RightAlignCell "日均交易"; |])
        
        let minVol = cfg.MinimalVolume
        let minVal = cfg.MinimalValue
        tt.AddPreTable(sprintf "最低交易量(vol)：%g 最低LP价值(val)：%g 结果上限(count)：%i" minVol minVal cfg.RecordCount)
        tt.AddPreTable("警告：请参考交易量，利润很高的不一定卖得掉")
        let corp = data.GetNpcCorporation(cfg.CmdLineAsString)
        data.GetLpStoreOffersByCorp(corp)
        |> Array.map (fun offer ->
            let bpRet = data.TryGetBp(offer.Offer.TypeId)
            let item = offer.Offer.MaterialItem

            let totalCost = //兑换需要
                (offer.Required
                |> Array.sumBy (fun m -> m.GetTotalPrice(cfg.MaterialPriceMode)))
                + offer.IskCost

            let sellPrice = 
                if bpRet.IsSome then
                    // 兑换数就是流程数，默认材料效率0
                    let bp = bpRet.Value.GetBpByRuns(offer.Offer.Quantity).ApplyMaterialEfficiency(0)
                    bp.GetTotalProductPrice(PriceFetchMode.SellWithTax) - bp.GetManufacturingPrice(cfg)
                else
                    offer.Offer.GetTotalPrice(PriceFetchMode.SellWithTax)

            let dailyVolume = 
                let item = if bpRet.IsSome then bpRet.Value.ProductItem else item
                data.GetItemTradeVolume(item)

            let offerStr = sprintf "%s*%g" item.Name offer.Offer.Quantity
            {|
                Name      = offerStr
                TotalCost = totalCost
                SellPrice = sellPrice
                Profit    = sellPrice - totalCost
                ProfitPerLp = (sellPrice - totalCost) / offer.LpCost
                Volume    = dailyVolume
                LpCost    = offer.LpCost
                Offer     = offer.Offer
            |})
        |> Array.filter (fun r -> (r.ProfitPerLp >= minVal) && (r.Volume >= minVol) )
        |> Array.sortByDescending (fun r ->
            //let weightedVolume = r.Volume / r.Offer.Quantity
            //r.ProfitPerLp * weightedVolume)
            r.ProfitPerLp)
        |> Array.truncate cfg.RecordCount
        |> Array.iter (fun r ->
            tt.AddRow(r.Name, r.Profit |> floor, r.ProfitPerLp |> floor, r.Volume |> floor))

        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("eveclearcache", "清空价格缓存（管理员）", "")>]
    member x.HandleRefreshCache(msgArg : CommandArgs) =
        failOnNonAdmin(msgArg)

        BotData.EveData.MarketPriceCache.PriceCacheCollection.Instance.Clear()

        msgArg.QuickMessageReply(sprintf "完毕")

    [<CommandHandlerMethodAttribute("eve矿物", "查询实时矿物价格", "")>]
    [<CommandHandlerMethodAttribute("em", "查询物品价格", "")>]
    [<CommandHandlerMethodAttribute("emr", "价格实时查询（市场中心API）", "")>]
    member x.HandleEveMarket(msgArg : CommandArgs) =
        let mutable argOverride = None
        let priceFunc = 
            match msgArg.Command with
            | Some "#em"  -> fun (t : EveType) -> data.GetItemPriceCached(t)
            | Some "#emr" -> fun (t : EveType) -> data.GetItemPrice(t)
            | Some "#eve矿物" -> 
                argOverride <- Some(MineralNames.Replace(',', '+'))
                fun (t : EveType) -> data.GetItemPriceCached(t)
            | _ -> failwithf "%A" msgArg.Command

        let t =
            let str =
                if argOverride.IsSome then argOverride.Value
                else String.Join(" ", msgArg.Arguments)
            er.Eval(str)

        let att = Utils.MarketUtils.GetPriceTable()
        match t with
        | Accumulator a ->
            for kv in a do 
                att.AddObject(kv.Key, kv.Value)
        | _ -> failwithf "求值失败，结果是%A" t

        let cfg = EveConfigParser()
        cfg.Parse(msgArg.Arguments)
        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(att))

    [<CommandHandlerMethodAttribute("eme", "EVE蓝图材料效率计算", "")>]
    member x.HandleME(msgArg : CommandArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(msgArg.Arguments)

        let bp = typeNameToBp(cfg.CmdLineAsString)
        let me0Price = bp.ApplyMaterialEfficiency(0).GetTotalMaterialPrice(PriceFetchMode.Sell)

        let att = AutoTextTable<int>(
                    [|
                        RightAlignCell "材料等级", fun me -> box(me)
                        RightAlignCell "节省", fun me -> 
                                        bp.ApplyMaterialEfficiency(me).GetTotalMaterialPrice(PriceFetchMode.Sell)
                                        |> (fun x -> (me0Price - x))
                                        |> box
                    |]
        )
        att.AddPreTable("直接材料总价：" + System.String.Format("{0:N0}", me0Price))

        for i = 0 to 10 do
            att.AddObject(i)

        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(att))

    [<CommandHandlerMethodAttribute("er", "EVE蓝图材料计算（可用表达式）", "")>]
    member x.HandleR(msgArg : CommandArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(msgArg.Arguments)

        let final = ItemAccumulator()
        let tt = TextTable.FromHeader([|"名称"; "数量";|])
        tt.AddPreTable(sprintf "输入效率：%i%% "cfg.InputMe)
        match er.Eval(cfg.CmdLineAsString) with
        | Number n ->
            failwithf "结算结果为数字:%g" n
        | Accumulator a ->
            for kv in a do 
                let q  = kv.Value

                let bp = tryTypeToBp(kv.Key)
                
                match bp with
                | _ when bp.IsSome && q > 0.0 -> 
                    // 需要计算
                    let bp = typeToBp(kv.Key).GetBpByRuns(q).ApplyMaterialEfficiency(cfg.InputMe)
                    tt.AddRow("产出："+bp.ProductItem.Name, bp.ProductQuantity)

                    for m in bp.Materials do
                        final.AddOrUpdate(m.MaterialItem, m.Quantity)

                | _ when q < 0.0 -> 
                    // 已有材料需要扣除
                    final.AddOrUpdate(kv.Key, kv.Value)
                | _ -> failwithf "不知道如何处理：%s * %g" kv.Key.Name kv.Value

            
        for kv in final do 
            tt.AddRow(kv.Key.Name, kv.Value)

        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("err", "EVE蓝图材料计算（可用表达式）", "")>]
    member x.HandleRR(msgArg : CommandArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(msgArg.Arguments)

        let final = ItemAccumulator()
        let tt = TextTable.FromHeader([|"名称"; "数量";|])
        tt.AddPreTable(sprintf "输入效率：%i%% 默认效率：%i%%"
            cfg.InputMe
            cfg.DerivativetMe
        )

        tt.AddPreTable(sprintf "展开行星材料：%b 展开反应公式：%b" cfg.ExpandPlanet cfg.ExpandReaction)

        let rec loop (bp : EveBlueprint) = 
            for m in bp.Materials do 
                let ret = data.TryGetBpByProduct(m.MaterialItem)
                if ret.IsSome && cfg.BpCanExpand(ret.Value) then
                    let bp = ret.Value.GetBpByItemNoCeil(m.Quantity).ApplyMaterialEfficiency(cfg.DerivativetMe)
                    loop(bp)
                else
                    final.AddOrUpdate(m.MaterialItem, m.Quantity)

        match er.Eval(cfg.CmdLineAsString) with
        | Number n ->
            failwithf "结算结果为数字:%g" n
        | Accumulator a ->
            for kv in a do 
                let q  = kv.Value
                let bp = typeToBp(kv.Key).GetBpByRuns(q).ApplyMaterialEfficiency(cfg.InputMe)
                tt.AddRow("产出："+ bp.ProductItem.Name, bp.ProductQuantity)

                loop(bp)
            
        for kv in final do 
            tt.AddRow(kv.Key.Name, kv.Value)

        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("errc", "EVE蓝图成本计算（可用表达式）", "")>]
    member x.HandleERRCV2(msgArg : CommandArgs) = 
        let cfg = EveConfigParser()
        cfg.Parse(msgArg.Arguments)

        let finalBp = 
            match er.Eval(cfg.CmdLineAsString) with
            | Number n ->
                failwithf "结算结果为数字:%g" n
            | Accumulator a ->
                /// 生成一个伪蓝图用于下游计算
                let os = ItemAccumulator<EveType>()
                let ms = ItemAccumulator<EveType>()
                let mutable bpTypeCheck = None

                for kv in a do
                    let t = kv.Key
                    let q = kv.Value
                    if q < 0.0 then failwith "不支持负数计算"

                    let bp = (typeToBp t).GetBpByRuns(q).ApplyMaterialEfficiency(cfg.InputMe)

                    if bpTypeCheck.IsNone then bpTypeCheck <- Some bp.Type
                    else
                        if bpTypeCheck.Value <> bp.Type then
                            invalidOp "蓝图类型不一致，无法计算，请拆分后重试"

                    os.AddOrUpdate(bp.ProductItem, bp.ProductQuantity)
                    for m in bp.Materials do
                        ms.AddOrUpdate(m.MaterialItem, m.Quantity)

                let ms = 
                    [| for kv in ms do yield {EveMaterial.TypeId = kv.Key.Id; EveMaterial.Quantity = kv.Value} |]
                let os = 
                    [| for kv in os do yield {EveMaterial.TypeId = kv.Key.Id; EveMaterial.Quantity = kv.Value} |]

                {   EveBlueprint.Materials = ms
                    EveBlueprint.Products = os
                    EveBlueprint.Id = Int32.MinValue
                    EveBlueprint.Type = bpTypeCheck.Value}

        let tt = TextTable.FromHeader([|"材料"; RightAlignCell "数量"; RightAlignCell <| cfg.MaterialPriceMode.ToString() ; RightAlignCell "生产"|])
        
        tt.AddPreTable(ToolWarning)
        tt.AddPreTable(sprintf "输入效率：%i%% 默认效率：%i%% 成本指数：%i%% 设施税率%i%%"
            cfg.InputMe
            cfg.DerivativetMe
            cfg.SystemCostIndex
            cfg.StructureTax )
        tt.AddPreTable(sprintf "展开行星材料：%b 展开反应公式：%b" cfg.ExpandPlanet cfg.ExpandReaction)

        tt.AddPreTable("产品：")
        let outTt = Utils.MarketUtils.GetPriceTable()
        for p in finalBp.Products do 
            outTt.AddObject(p.MaterialItem, p.Quantity)
        tt.AddPreTable(outTt)
        tt.AddPreTable("材料：")

        let rec getRootMaterialsPrice (bp : EveBlueprint) = 
            let mutable sum = 0.0

            for m in bp.Materials do 
                let price = m.GetTotalPrice(cfg.MaterialPriceMode)
                let fee = bp.GetManufacturingFee(cfg)
                sum <- sum + fee 

                let ret = data.TryGetBpByProduct(m.MaterialItem)
                if ret.IsSome && cfg.BpCanExpand(ret.Value) then
                    let bp = ret.Value.GetBpByItemNoCeil(m.Quantity).ApplyMaterialEfficiency(cfg.DerivativetMe)
                    sum <- sum + getRootMaterialsPrice bp
                else
                    sum <- sum + price
            sum

        let baseFee = finalBp.GetManufacturingFee(cfg)
        tt.AddRow("制造费用", 1, baseFee |> HumanReadableFloat |> RightAlignCell, baseFee |> HumanReadableFloat |> RightAlignCell)

        let mutable optCost = baseFee
        let mutable allCost = baseFee

        // 所有材料
        // 材料名称 数量 售价（小计） 制造成本（小计） 最佳成本（小计）
        for m in finalBp.Materials do 
            let amount = m.Quantity
            let name   = m.MaterialItem.Name
            let buy    = m.GetTotalPrice(cfg.MaterialPriceMode)
                
            let ret = data.TryGetBpByProduct(m.MaterialItem)
            if ret.IsSome && cfg.BpCanExpand(ret.Value) then
                let bp = ret.Value.GetBpByItemNoCeil(amount).ApplyMaterialEfficiency(cfg.DerivativetMe)
                let cost = getRootMaterialsPrice bp
                optCost <- optCost + (if (cost >= buy) && (buy <> 0.0) then buy else cost)
                allCost <- allCost + cost
                tt.AddRow(name, amount, buy |> HumanReadableFloat |> RightAlignCell , cost |> HumanReadableFloat |> RightAlignCell)
            else
                optCost <- optCost + buy
                allCost <- allCost + buy
                tt.AddRow(name, amount, buy |> HumanReadableFloat |> RightAlignCell, "--" |> RightAlignCell)

        let sell = finalBp.GetTotalProductPrice(PriceFetchMode.Sell)
        let sellt= finalBp.GetTotalProductPrice(PriceFetchMode.SellWithTax)

        tt.AddRowPadding("卖出/税后", RightAlignCell "--", sell |> HumanReadableFloat |> RightAlignCell, sellt |> HumanReadableFloat |> RightAlignCell)
        tt.AddRowPadding("材料/最佳", RightAlignCell "--", allCost |> HumanReadableFloat |> RightAlignCell, optCost |> HumanReadableFloat |> RightAlignCell)
        tt.AddRowPadding("税后利润", RightAlignCell "--", sellt - allCost |> HumanReadableFloat |> RightAlignCell, sellt - optCost |> HumanReadableFloat |> RightAlignCell)

        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("EVE采矿", "EVE挖矿利润", "")>]
    [<CommandHandlerMethodAttribute("EVE挖矿", "EVE挖矿利润", "")>]
    member x.HandleOreMining(msgArg : CommandArgs) =
        let mineSpeed = 10.0 // m^3/s
        let refineYield = 0.70

        let tt = TextTable.FromHeader([| "矿石"; RightAlignCell "秒利润"; 
                                         "冰矿"; RightAlignCell "秒利润";
                                         "月矿"; RightAlignCell "秒利润";
                                         "导管"; RightAlignCell "秒利润"; |])

        tt.AddPreTable(ToolWarning)
        tt.AddPreTable(sprintf "采集能力：%g m3/s 精炼效率:%g"
            mineSpeed
            refineYield
        )
            
        let getSubTypes (names : string) = 
            names.Split(',')
            |> Array.map (fun name ->
                let item = data.GetItem(name)
                let info = data.GetRefineInfo(item)
                let refinePerSec = mineSpeed / info.InputType.Volume / info.RefineUnit
                let price =
                    info.Yields
                    |> Array.sumBy (fun m -> 
                        m.Quantity * refinePerSec * refineYield * data.GetItemPriceCached(m.TypeId).Sell)
                name, price|> ceil )
            |> Array.sortByDescending snd

        let moon= getSubTypes MoonNames
        let ice = getSubTypes IceNames
        let ore = getSubTypes OreNames
        let tore= getSubTypes TriglavianOreNames

        let tryGetRow (arr : (string * float) [])  (id : int)  = 
            if id <= arr.Length - 1 then
                let n,p = arr.[id]
                (box n, box p)
            else
                (box "--", box <| RightAlignCell "--")

        let rowMax = (max (max ice.Length moon.Length) ore.Length) - 1
        for i = 0 to rowMax do 
            let eon, eop = tryGetRow ore i
            let ein, eip = tryGetRow ice i
            let emn, emp = tryGetRow moon i
            let etn, etp = tryGetRow tore i

            tt.AddRow(eon, eop, ein, eip, emn, emp, etn, etp)

        use ret = msgArg.OpenResponse(true)
        ret.Write(tt)
    
    [<CommandHandlerMethodAttribute("EVE舰船II", "T2舰船制造总览", "")>]
    [<CommandHandlerMethodAttribute("EVE舰船", "T2舰船制造总览", "")>]
    [<CommandHandlerMethodAttribute("EVE组件", "T2和旗舰组件制造总览", "")>]
    [<CommandHandlerMethodAttribute("EVE种菜", "EVE种菜利润", "")>]
    [<CommandHandlerMethodAttribute("EVE装备II", "EVET2装备利润", "需要关键词")>]
    member x.HandleManufacturingOverview(msgArg : CommandArgs) = 
        let cfg = EveConfigParser()
        cfg.Parse(msgArg.Arguments)

        use ret = msgArg.OpenResponse(cfg.IsImageOutput)

        let filterFunc : (EveBlueprint -> bool) = 
            match msgArg.Command with
            | Some "#eve组件"  -> fun bp ->
                (bp.Type = BlueprintType.Manufacturing) 
                    && ( (bp.ProductItem.GroupId = 334) // Tech2ComponentGroupId
                            || (bp.ProductItem.GroupId = 873) )    // CapitalComponentGroupId
            | Some "#eve种菜" -> fun bp -> 
                (bp.Type = BlueprintType.Planet) 
            | Some "#eve舰船" -> fun bp ->
                (bp.Type = BlueprintType.Manufacturing)
                    && (bp.ProductItem.CategoryId = 6) // 6 = 舰船
                    && (bp.ProductItem.MetaGroupId <> 2) // T1
                    && (let mg = bp.ProductItem.MarketGroup
                        mg.IsSome && (not <| mg.Value.Name.Contains("特别")) )
            | Some "#eve舰船ii" -> fun bp ->
                (bp.Type = BlueprintType.Manufacturing)
                    && (bp.ProductItem.CategoryId = 6) // 6 = 舰船
                    && (bp.ProductItem.MetaGroupId = 2) // T2
                    && (let mg = bp.ProductItem.MarketGroup
                        mg.IsSome && (not <| mg.Value.Name.Contains("特别")) )
            | Some "#eve装备ii" -> fun bp ->
                if cfg.CommandLine.Length <> 1 then ret.FailWith("需要一个装备名称关键词")
                let keyword = cfg.CommandLine.[0]
                if keyword.Length < 2 then ret.FailWith("至少2个字")
                if keyword.Contains("I") then ret.FailWith("emmm 想看全部T2还是别想了")
                (bp.Type = BlueprintType.Manufacturing)
                    && (bp.ProductItem.CategoryId = 7) // 装备
                    && (bp.ProductItem.MetaGroupId = 2) // T2
                    && (bp.ProductItem.Name.Contains(keyword))
            | _ -> failwithf "%A" msgArg.Command


        let pmStr = cfg.MaterialPriceMode.ToString()

        
        ret.WriteLine(ToolWarning)
        data.GetBps()
        |> Seq.filter filterFunc
        |> (fun seq ->
            if (Seq.length seq) = 0 then ret.FailWith("无符合要求的蓝图信息")
            seq )
        |> Seq.map (fun ps ->
            let ps = ps.ApplyMaterialEfficiency(cfg.InputMe)
            let name = ps.ProductItem.Name
            let cost = ps.GetManufacturingPrice(cfg)
            let sellWithTax = ps.GetTotalProductPrice(PriceFetchMode.SellWithTax)
            let volume = data.GetItemTradeVolume(ps.ProductItem)
            {|
                Name = name
                TypeGroup = ps.ProductItem.TypeGroup
                Cost = cost
                Quantity = ps.ProductQuantity
                Sell = ps.GetTotalProductPrice(PriceFetchMode.Sell)
                Profit = sellWithTax - cost
                Volume = volume
            |} )
        |> Seq.sortByDescending (fun x -> x.Profit)
        |> Seq.groupBy (fun x -> x.TypeGroup)
        |> Seq.iter (fun (group, data) -> 
            ret.WriteLine(">>{0}<<", group.Name)
            let tt = TextTable.FromHeader([|"方案"
                                            RightAlignCell "出售价格/无税卖出"
                                            RightAlignCell ("生产成本/" + pmStr)
                                            RightAlignCell "含税利润"
                                            RightAlignCell "日均交易"|])
            for x in data do 
                tt.AddRow(x.Name,
                          x.Sell |> HumanReadableFloat |> RightAlignCell,
                          x.Cost |> HumanReadableFloat |> RightAlignCell,
                          x.Profit |> HumanReadableFloat |> RightAlignCell,
                          x.Volume |> HumanReadableFloat |> RightAlignCell)
            ret.Write(tt)
            ret.WriteEmptyLine()
        )