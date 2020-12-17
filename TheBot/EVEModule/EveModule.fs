namespace TheBot.Module.EveModule

open System

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

open BotData.CommonModule.Recipe
open BotData.EveData.Utils
open BotData.EveData.EveType
open BotData.EveData.Process

open TheBot.Utils.HandlerUtils
open TheBot.Utils.RecipeRPN

open TheBot.Module.EveModule.Utils.Helpers
open TheBot.Module.EveModule.Utils.Config
open TheBot.Module.EveModule.Utils.Data
open TheBot.Module.EveModule.Utils.Extensions

type EveModule() =
    inherit CommandHandlerBase()

    let data = DataBundle.Instance
    let pm = EveProcessManager.Default

    let ToolWarning = "价格有延迟，算法不稳定，市场有风险, 投资需谨慎"
    let er = EveExpression.EveExpression()

    [<CommandHandlerMethodAttribute("eveLp", "EVE LP兑换计算", "#evelp 军团名")>]
    member x.HandleEveLp(msgArg : CommandArgs) =
        let cfg = Utils.LpUtils.LpConfigParser()
        cfg.Parse(msgArg.Arguments)

        let tt = TextTable("兑换", RightAlignCell "利润", RightAlignCell "利润/LP", RightAlignCell "日均交易")
        
        let minVol = cfg.MinimalVolume
        let minVal = cfg.MinimalValue
        tt.AddPreTable(sprintf "最低交易量(vol)：%g 最低LP价值(val)：%g 结果上限(count)：%i" minVol minVal cfg.RecordCount)
        tt.AddPreTable("警告：请参考交易量，利润很高的不一定卖得掉")
        let corp = data.GetNpcCorporation(cfg.CmdLineAsString)
        data.GetLpStoreOffersByCorp(corp)
        |> Array.map (fun lpOffer ->
            let proc = lpOffer.CastProcess()
            let itemOffer = proc.GetFirstProduct()

            let totalCost =
                let inputCost = proc.Input
                                |> Array.sumBy (fun mr -> mr.Item.GetPrice(cfg.MaterialPriceMode) * mr.Quantity)
                inputCost + lpOffer.IskCost

            let dailyVolume, sellPrice = 
                if itemOffer.Item.IsBlueprint then
                    let proc = pm.GetRecipe(itemOffer)
                    let price = proc.GetTotalProductPrice(PriceFetchMode.SellWithTax)
                                - proc.GetInstallationCost(cfg)
                    data.GetItemTradeVolume(proc.Process.GetFirstProduct().Item), price
                else
                    let price = 
                        proc.Output
                        |> Array.sumBy (fun mr -> mr.Item.GetPrice(PriceFetchMode.SellWithTax) * mr.Quantity)
                    data.GetItemTradeVolume(itemOffer.Item), price

            let offerStr = sprintf "%s*%g" itemOffer.Item.Name itemOffer.Quantity
            {|
                Name      = offerStr
                TotalCost = totalCost
                SellPrice = sellPrice
                Profit    = sellPrice - totalCost
                ProfitPerLp = (sellPrice - totalCost) / lpOffer.LpCost
                Volume    = dailyVolume
                LpCost    = lpOffer.LpCost
                Offer     = itemOffer
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

    [<CommandHandlerMethodAttribute("eve矿物", "查询矿物价格", "")>]
    [<CommandHandlerMethodAttribute("em", "查询物品价格", "")>]
    member x.HandleEveMarket(msgArg : CommandArgs) =
        let mutable argOverride = None
        if msgArg.CommandName = "eve矿物" then
            argOverride <- Some(MineralNames.Replace(',', '+'))
        let t =
            let str =
                if argOverride.IsSome then argOverride.Value
                else String.Join(" ", msgArg.Arguments)
            er.Eval(str)

        let att = Utils.MarketUtils.EveMarketPriceTable()
        match t with
        | Accumulator a ->
            for mr in a do 
                att.AddObject(mr.Item, mr.Quantity)
        | _ -> msgArg.AbortExecution(InputError, sprintf "求值失败，结果是%A" t)

        let cfg = EveConfigParser()
        cfg.Parse(msgArg.Arguments)
        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(att))

    [<CommandHandlerMethodAttribute("eme", "EVE蓝图材料效率计算", "")>]
    member x.HandleME(msgArg : CommandArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(msgArg.Arguments)

        let item = data.TryGetItem(cfg.CmdLineAsString)
        if item.IsNone then msgArg.AbortExecution(InputError, "找不到物品：{0}", cfg.CmdLineAsString)

        let proc0 = pm.TryGetRecipeMe(item.Value, ByRun 1.0, 0)
        if proc0.IsNone then msgArg.AbortExecution(InputError, "找不到蓝图：{0}", cfg.CmdLineAsString)

        let me0Price = proc0.Value.GetTotalMaterialPrice(PriceFetchMode.Sell)

        let tt = TextTable(RightAlignCell "材料等级", RightAlignCell "节省")
        tt.AddPreTable("直接材料总价：" + System.String.Format("{0:N0}", me0Price))

        for me = 0 to 10 do
            let cost = pm.TryGetRecipeMe(item.Value, ByRun 1.0, me)
                         .Value.GetTotalMaterialPrice(PriceFetchMode.Sell)
            let save = me0Price - cost
            tt.AddRow(me, save)

        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("er", "EVE蓝图材料计算（可用表达式）", "")>]
    member x.HandleR(msgArg : CommandArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(msgArg.Arguments)

        let final = ItemAccumulator<EveType>()
        let tt = TextTable("名称", "数量")
        tt.AddPreTable(sprintf "输入效率：%i%% "cfg.InputMe)
        match er.Eval(cfg.CmdLineAsString) with
        | Number n ->
            msgArg.AbortExecution(InputError, "结算结果为数字: {0}", n)
        | Accumulator a ->
            let pm = EveProcessManager(cfg)
            for mr in a do 
                let proc = pm.TryGetRecipeMe(mr.Item, ByRun mr.Quantity)
                match proc with
                | _ when proc.IsSome && mr.Quantity > 0.0 -> 
                    let product = proc.Value.Process.GetFirstProduct()
                    tt.AddRow("产出："+product.Item.Name, product.Quantity)

                    for m in proc.Value.Process.Input do
                        final.Update(m)
                | _ when mr.Quantity < 0.0 -> 
                    // 已有材料需要扣除
                    final.Update(mr)
                | _ -> 
                    msgArg.AbortExecution(ModuleError, "不知道如何处理：{0} * {1}", mr.Item.Name, mr.Quantity)

        for mr in final do 
            tt.AddRow(mr.Item.Name, mr.Quantity)

        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("err", "EVE蓝图材料计算（可用表达式）", "")>]
    member x.HandleRR(msgArg : CommandArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(msgArg.Arguments)

        let final = ItemAccumulator<EveType>()
        let tt = TextTable("名称", "数量")
        tt.AddPreTable(sprintf "输入效率：%i%% 默认效率：%i%%"
            cfg.InputMe
            cfg.DerivativetMe
        )

        tt.AddPreTable(sprintf "展开行星材料：%b 展开反应公式：%b" cfg.ExpandPlanet cfg.ExpandReaction)

        match er.Eval(cfg.CmdLineAsString) with
        | Number n ->
            msgArg.AbortExecution(InputError, "结算结果为数字: {0}", n)
        | Accumulator a ->
            let pm = EveProcessManager(cfg)
            for mr in a do 
                let proc = pm.TryGetRecipeRecMe(mr.Item, ByRun mr.Quantity)
                if proc.IsNone then
                    msgArg.AbortExecution(InputError, "找不到配方：{0}", mr.Item.Name)
                let finalProc = proc.Value.FinalProcess
                let product = finalProc.Process.GetFirstProduct()

                tt.AddRow("产出："+ product.Item.Name, product.Quantity)

                for m in finalProc.Process.Input do
                    final.Update(m)
            
        for mr in final do 
            tt.AddRow(mr.Item.Name, mr.Quantity)

        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("errc", "EVE蓝图成本计算（只计算一个物品）", "")>]
    member x.HandleERRCV2(msgArg : CommandArgs) = 
        let cfg = EveConfigParser()
        cfg.Parse(msgArg.Arguments)

        match er.Eval(cfg.CmdLineAsString) with
        | Number n ->
            msgArg.AbortExecution(InputError, "结算结果为数字: {0}", n)
        | Accumulator a ->
            let mr = a |> Seq.tryHead
            if mr.IsNone then
                msgArg.AbortExecution(InputError, "没有可供计算的物品")
            let pm = EveProcessManager(cfg)
            let proc = pm.TryGetRecipeMe(mr.Value.Item, ByRun mr.Value.Quantity)
            if proc.IsNone then
                msgArg.AbortExecution(InputError, "找不到配方:{0}", mr.Value.Item.Name)

            let tt = TextTable(LeftAlignCell "材料",
                               RightAlignCell "数量",
                               RightAlignCell (cfg.MaterialPriceMode.ToString()),
                               RightAlignCell "生产")
            tt.AddPreTable(ToolWarning)
            tt.AddPreTable(sprintf "输入效率：%i%% 默认效率：%i%% 成本指数：%i%% 设施税率%i%%"
                cfg.InputMe cfg.DerivativetMe cfg.SystemCostIndex cfg.StructureTax )
            tt.AddPreTable(sprintf "展开行星材料：%b 展开反应公式：%b" cfg.ExpandPlanet cfg.ExpandReaction)

            tt.AddPreTable("产品：")
            let priceTable = Utils.MarketUtils.EveMarketPriceTable()
            let product = proc.Value.Process.GetFirstProduct()
            priceTable.AddObject(product.Item, product.Quantity)
            tt.AddPreTable(priceTable)

            tt.AddPreTable("材料：")

            let installFee = proc.Value.GetInstallationCost(cfg)
            tt.AddRow("制造费用", RightAlignCell "--", RightAlignCell (HumanReadableFloat installFee), RightAlignCell "--")

            let mutable optCost = installFee
            let mutable allCost = installFee

            for mr in proc.Value.Process.Input do 
                let price = mr.Item.GetPrice(cfg.MaterialPriceMode) * mr.Quantity
                let mrProc = pm.TryGetRecipeRecMe(mr.Item, ByItem mr.Quantity, cfg.DerivativetMe, cfg.DerivativetMe)
                if mrProc.IsNone then
                    optCost <- optCost + price
                    allCost <- allCost + price
                    tt.AddRow(mr.Item.Name, mr.Quantity,
                                RightAlignCell (HumanReadableFloat price),
                                RightAlignCell "--")
                else
                    let mrInstall = mrProc.Value.FinalProcess.GetInstallationCost(cfg)
                    let mrCost = mrProc.Value.FinalProcess.GetTotalMaterialPrice(cfg.MaterialPriceMode)
                    let mrAll = mrInstall + mrCost
                    allCost <- allCost + mrAll
                    optCost <- optCost + (if (mrAll >= price) && (price <> 0.0) then price else mrAll)
                    tt.AddRow(mr.Item.Name, mr.Quantity,
                                RightAlignCell (HumanReadableFloat price),
                                RightAlignCell (HumanReadableFloat mrAll))

            let sell = proc.Value.GetTotalProductPrice(PriceFetchMode.Sell)
            let sellWithTax = proc.Value.GetTotalProductPrice(PriceFetchMode.SellWithTax)
            tt.AddRow("卖出/税后", RightAlignCell "--",
                                   RightAlignCell (HumanReadableFloat sell),
                                   RightAlignCell (HumanReadableFloat sellWithTax))
            tt.AddRow("材料/最佳", RightAlignCell "--",
                                   RightAlignCell (HumanReadableFloat allCost),
                                   RightAlignCell (HumanReadableFloat optCost))
            tt.AddRow("税后 利润", RightAlignCell "--",
                                   RightAlignCell (HumanReadableFloat (sellWithTax - allCost)),
                                   RightAlignCell (HumanReadableFloat (sellWithTax - optCost)))

            using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun ret -> ret.Write(tt))

    [<CommandHandlerMethodAttribute("EVE采矿", "EVE挖矿利润", "")>]
    [<CommandHandlerMethodAttribute("EVE挖矿", "EVE挖矿利润", "")>]
    member x.HandleOreMining(msgArg : CommandArgs) =
        let mineSpeed = 10.0 // m^3/s
        let refineYield = 0.70

        let tt = TextTable( "矿石", RightAlignCell "秒利润",
                            "冰矿", RightAlignCell "秒利润",
                            "月矿", RightAlignCell "秒利润",
                            "导管", RightAlignCell "秒利润" )

        tt.AddPreTable(ToolWarning)
        tt.AddPreTable(sprintf "采集能力：%g m3/s 精炼效率:%g"
            mineSpeed
            refineYield
        )
            
        let getSubTypes (names : string) = 
            names.Split(',')
            |> Array.map (fun name ->
                let item = data.GetItem(name)
                let proc = RefineProcessCollection.Instance.GetProcessFor(item).Process
                let input = proc.Input.[0]
                let refinePerSec = mineSpeed / input.Item.Volume / input.Quantity
                let price =
                    proc.Output
                    |> Array.sumBy (fun m -> 
                        m.Quantity * refinePerSec * refineYield * m.Item.GetPriceInfo().Sell)
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

        use ret = msgArg.OpenResponse(ForceImage)
        ret.Write(tt)
    
    [<CommandHandlerMethodAttribute("EVE舰船II", "T2舰船制造总览", "")>]
    [<CommandHandlerMethodAttribute("EVE舰船", "T2舰船制造总览", "")>]
    [<CommandHandlerMethodAttribute("EVE组件", "T2和旗舰组件制造总览", "")>]
    [<CommandHandlerMethodAttribute("EVE种菜", "EVE种菜利润", "")>]
    [<CommandHandlerMethodAttribute("EVE装备II", "EVET2装备利润", "需要关键词")>]
    member x.HandleManufacturingOverview(msgArg : CommandArgs) = 
        let cfg = EveConfigParser()
        cfg.RegisterOption("by", "")
        cfg.Parse(msgArg.Arguments)

        use ret = msgArg.OpenResponse(cfg.IsImageOutput)
        ret.WriteLine(ToolWarning)

        let filterFunc : (EveProcess -> bool) = 
            match msgArg.CommandName with // 注意小写匹配
            | "eve组件"  -> fun bp ->
                (bp.Type = ProcessType.Manufacturing) 
                    && ( (bp.Process.GetFirstProduct().Item.GroupId = 334) // Tech2ComponentGroupId
                            || (bp.Process.GetFirstProduct().Item.GroupId = 873) )    // CapitalComponentGroupId
            | "eve种菜" -> fun bp -> 
                (bp.Type = ProcessType.Planet) 
            | "eve舰船" -> fun bp ->
                (bp.Type = ProcessType.Manufacturing)
                    && (bp.Process.GetFirstProduct().Item.CategoryId = 6) // 6 = 舰船
                    && (bp.Process.GetFirstProduct().Item.MetaGroupId <> 2) // T1
                    && (let mg = bp.Process.GetFirstProduct().Item.MarketGroup
                        mg.IsSome && (not <| mg.Value.Name.Contains("特别")) )
            | "eve舰船ii" -> fun bp ->
                (bp.Type = ProcessType.Manufacturing)
                    && (bp.Process.GetFirstProduct().Item.CategoryId = 6) // 6 = 舰船
                    && (bp.Process.GetFirstProduct().Item.MetaGroupId = 2) // T2
                    && (let mg = bp.Process.GetFirstProduct().Item.MarketGroup
                        mg.IsSome && (not <| mg.Value.Name.Contains("特别")) )
            | "eve装备ii" -> 
                let isGroup = cfg.GetValue("by") = "group"

                if isGroup then
                    ret.WriteLine("按装备名匹配")
                else
                    ret.WriteLine("按名称匹配，按组名匹配请使用by:group")

                let keyword = 
                    if cfg.CommandLine.Length = 0 then ret.AbortExecution(InputError, "需要一个装备名称关键词")
                    cfg.CommandLine.[0]

                if keyword.Length < 2 then ret.AbortExecution(InputError, "至少2个字")
                if keyword.Contains("I") then ret.AbortExecution(InputError, "emmm 想看全部T2还是别想了")

                let allowCategoryId = [|7; 18; 8;|] |> Set // 装备，无人机，弹药

                fun bp ->
                    (bp.Type = ProcessType.Manufacturing)
                        && (if isGroup then 
                                bp.Process.GetFirstProduct().Item.TypeGroup.Name.Contains(keyword)
                            else
                                bp.Process.GetFirstProduct().Item.Name.Contains(keyword)   )
                        && (allowCategoryId.Contains(bp.Process.GetFirstProduct().Item.CategoryId)) // 装备
                        && (bp.Process.GetFirstProduct().Item.MetaGroupId = 2) // T2

            | other -> msgArg.AbortExecution(ModuleError, "不应发生匹配:{0}", other)


        let pmStr = cfg.MaterialPriceMode.ToString()

        let pm = EveProcessManager(cfg)
        BlueprintCollection.Instance.GetAllProcesses()
        |> Seq.filter filterFunc
        |> (fun seq ->
            if (Seq.length seq) = 0 then ret.AbortExecution(InputError, "无符合要求的蓝图信息")
            seq )
        |> Seq.map (fun ps ->
            let proc = pm.ApplyProcess(ps, ByRun 1.0)
            let product = proc.Process.GetFirstProduct()

            let cost = proc.GetTotalMaterialPrice(cfg.MaterialPriceMode) + proc.GetInstallationCost(cfg)
            let sellWithTax = ps.GetTotalProductPrice(PriceFetchMode.SellWithTax)
            let volume = data.GetItemTradeVolume(product.Item)
            {|
                Name = product.Item.Name
                TypeGroup = product.Item.TypeGroup
                Cost = cost
                Quantity = product.Quantity
                Sell = ps.GetTotalProductPrice(PriceFetchMode.Sell)
                Profit = sellWithTax - cost
                Volume = volume
            |} )
        |> Seq.sortByDescending (fun x -> x.Profit)
        |> Seq.groupBy (fun x -> x.TypeGroup)
        |> Seq.iter (fun (group, data) -> 
            ret.WriteLine(">>{0}<<", group.Name)
            let tt = TextTable( "方案",
                                RightAlignCell "出售价格/无税卖出",
                                RightAlignCell ("生产成本/" + pmStr),
                                RightAlignCell "含税利润",
                                RightAlignCell "日均交易" )
            for x in data do 
                tt.AddRow(x.Name,
                          x.Sell |> HumanReadableFloat |> RightAlignCell,
                          x.Cost |> HumanReadableFloat |> RightAlignCell,
                          x.Profit |> HumanReadableFloat |> RightAlignCell,
                          x.Volume |> HumanReadableFloat |> RightAlignCell)
            ret.Write(tt)
            ret.WriteEmptyLine()
        )


    [<CommandHandlerMethodAttribute("evesci", "EVE星系成本指数查询", "")>]
    member x.HandleSci(msgArg : CommandArgs) = 
        let sc = BotData.EveData.SolarSystems.SolarSystemCollection.Instance
        let scc = BotData.EveData.SystemCostIndexCache.SystemCostIndexCollection.Instance
        let cfg = EveConfigParser()
        cfg.Parse(msgArg.Arguments)

        let tt = TextTable("星系", "制造%", "材料%", "时间%", "拷贝%", "发明%", "反应%")

        for arg in cfg.CommandLine do 
            let sys = sc.TryGetBySolarSystem(arg)
            if sys.IsNone then
                tt.AddPreTable(sprintf "%s不是有效星系名称" arg)
            else
                let sci = scc.TryGetBySystem(sys.Value)
                if sci.IsNone then
                    tt.AddPreTable(sprintf "没有%s的指数信息" arg)
                else
                    let sci = sci.Value
                    tt.AddRow(arg, 100.0 * sci.Manufacturing,
                                100.0 * sci.ResearcMaterial,
                                100.0 * sci.ResearchTime,
                                100.0 * sci.Copying,
                                100.0 * sci.Invention,
                                100.0 * sci.Reaction)

        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun ret -> ret.Write(tt))