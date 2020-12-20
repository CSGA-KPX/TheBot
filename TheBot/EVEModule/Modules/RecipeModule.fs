namespace TheBot.Module.EveModule

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

open BotData.CommonModule.Recipe
open BotData.EveData.Utils
open BotData.EveData.EveType
open BotData.EveData.Process

open TheBot.Utils.RecipeRPN

open TheBot.Module.EveModule.Utils.Helpers
open TheBot.Module.EveModule.Utils.Config
open TheBot.Module.EveModule.Utils.Data
open TheBot.Module.EveModule.Utils.Extensions


type EveRecipeModule() =
    inherit CommandHandlerBase()

    let data = DataBundle.Instance
    let pm = EveProcessManager.Default

    let er = EveExpression.EveExpression()

    [<CommandHandlerMethodAttribute("eme", "EVE蓝图材料效率计算", "")>]
    member x.HandleME(cmdArg : CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.Arguments)

        let item = data.TryGetItem(cfg.CmdLineAsString)

        if item.IsNone
        then cmdArg.AbortExecution(InputError, "找不到物品：{0}", cfg.CmdLineAsString)

        let proc0 =
            pm.TryGetRecipeMe(item.Value, ByRun 1.0, 0)

        if proc0.IsNone
        then cmdArg.AbortExecution(InputError, "找不到蓝图：{0}", cfg.CmdLineAsString)

        let me0Price =
            proc0.Value.GetTotalMaterialPrice(PriceFetchMode.Sell)

        let tt =
            TextTable(RightAlignCell "材料等级", RightAlignCell "节省")

        tt.AddPreTable(
            "直接材料总价："
            + System.String.Format("{0:N0}", ceil me0Price)
        )
        for me = 0 to 10 do
            let cost =
                pm
                    .TryGetRecipeMe(item.Value, ByRun 1.0, me)
                    .Value.GetTotalMaterialPrice(PriceFetchMode.Sell)

            let save = me0Price - cost |> ceil
            tt.AddRow(me, save)

        using (cmdArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("er", "EVE蓝图材料计算（可用表达式）", "")>]
    member x.HandleR(cmdArg : CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.Arguments)

        let final = ItemAccumulator<EveType>()
        let tt = TextTable("名称", "数量")
        tt.AddPreTable(sprintf "输入效率：%i%% " cfg.InputMe)

        match er.Eval(cfg.CmdLineAsString) with
        | Number n -> cmdArg.AbortExecution(InputError, "结算结果为数字: {0}", n)
        | Accumulator a ->
            let pm = EveProcessManager(cfg)

            for mr in a do
                let proc =
                    pm.TryGetRecipeMe(mr.Item, ByRun mr.Quantity)

                match proc with
                | _ when proc.IsSome && mr.Quantity > 0.0 ->
                    let product = proc.Value.Process.GetFirstProduct()
                    tt.AddRow("产出：" + product.Item.Name, product.Quantity)

                    for m in proc.Value.Process.Input do
                        final.Update(m)
                | _ when mr.Quantity < 0.0 ->
                    // 已有材料需要扣除
                    final.Update(mr)
                | _ -> cmdArg.AbortExecution(ModuleError, "不知道如何处理：{0} * {1}", mr.Item.Name, mr.Quantity)

        for mr in final do
            tt.AddRow(mr.Item.Name, mr.Quantity)

        using (cmdArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("err", "EVE蓝图材料计算（可用表达式）", "")>]
    member x.HandleRR(cmdArg : CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.Arguments)

        let final = ItemAccumulator<EveType>()
        let tt = TextTable("名称", "数量")
        tt.AddPreTable(sprintf "输入效率：%i%% 默认效率：%i%%" cfg.InputMe cfg.DerivativetMe)

        tt.AddPreTable(sprintf "展开行星材料：%b 展开反应公式：%b" cfg.ExpandPlanet cfg.ExpandReaction)

        match er.Eval(cfg.CmdLineAsString) with
        | Number n -> cmdArg.AbortExecution(InputError, "结算结果为数字: {0}", n)
        | Accumulator a ->
            let pm = EveProcessManager(cfg)

            for mr in a do
                let proc =
                    pm.TryGetRecipeRecMe(mr.Item, ByRun mr.Quantity)

                if proc.IsNone then cmdArg.AbortExecution(InputError, "找不到配方：{0}", mr.Item.Name)

                let finalProc = proc.Value.FinalProcess
                let product = finalProc.Process.GetFirstProduct()

                tt.AddRow("产出：" + product.Item.Name, product.Quantity)

                for m in finalProc.Process.Input do
                    final.Update(m)

        for mr in final do
            tt.AddRow(mr.Item.Name, mr.Quantity)

        using (cmdArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("errc", "EVE蓝图成本计算（只计算一个物品）", "")>]
    member x.HandleERRCV2(cmdArg : CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.Arguments)

        match er.Eval(cfg.CmdLineAsString) with
        | Number n -> cmdArg.AbortExecution(InputError, "结算结果为数字: {0}", n)
        | Accumulator a ->
            let mr = a |> Seq.tryHead

            if mr.IsNone then cmdArg.AbortExecution(InputError, "没有可供计算的物品")

            let pm = EveProcessManager(cfg)

            let proc =
                pm.TryGetRecipeMe(mr.Value.Item, ByRun mr.Value.Quantity)

            if proc.IsNone
            then cmdArg.AbortExecution(InputError, "找不到配方:{0}", mr.Value.Item.Name)

            let tt =
                TextTable(
                    LeftAlignCell "材料",
                    RightAlignCell "数量",
                    RightAlignCell(cfg.MaterialPriceMode.ToString()),
                    RightAlignCell "生产"
                )

            tt.AddPreTable(ToolWarning)

            tt.AddPreTable(
                sprintf
                    "输入效率：%i%% 默认效率：%i%% 成本指数：%i%% 设施税率%i%%"
                    cfg.InputMe
                    cfg.DerivativetMe
                    cfg.SystemCostIndex
                    cfg.StructureTax
            )

            tt.AddPreTable(sprintf "展开行星材料：%b 展开反应公式：%b" cfg.ExpandPlanet cfg.ExpandReaction)

            tt.AddPreTable("产品：")
            let priceTable = Utils.MarketUtils.EveMarketPriceTable()
            let product = proc.Value.Process.GetFirstProduct()
            priceTable.AddObject(product.Item, product.Quantity)
            tt.AddPreTable(priceTable)

            tt.AddPreTable("材料：")

            let installFee = proc.Value.GetInstallationCost(cfg)
            tt.AddRow("制造费用", RightAlignCell "--", RightAlignCell(HumanReadableFloat installFee), RightAlignCell "--")

            let mutable optCost = installFee
            let mutable allCost = installFee

            for mr in proc.Value.Process.Input do
                let price =
                    mr.Item.GetPrice(cfg.MaterialPriceMode)
                    * mr.Quantity

                let mrProc =
                    pm.TryGetRecipeRecMe(mr.Item, ByItem mr.Quantity, cfg.DerivativetMe, cfg.DerivativetMe)

                if mrProc.IsNone then
                    optCost <- optCost + price
                    allCost <- allCost + price
                    tt.AddRow(mr.Item.Name, mr.Quantity, RightAlignCell(HumanReadableFloat price), RightAlignCell "--")
                else
                    let mrInstall =
                        mrProc.Value.FinalProcess.GetInstallationCost(cfg)

                    let mrCost =
                        mrProc.Value.FinalProcess.GetTotalMaterialPrice(cfg.MaterialPriceMode)

                    let mrAll = mrInstall + mrCost
                    allCost <- allCost + mrAll

                    optCost <-
                        optCost
                        + (if (mrAll >= price) && (price <> 0.0) then price else mrAll)

                    tt.AddRow(
                        mr.Item.Name,
                        mr.Quantity,
                        RightAlignCell(HumanReadableFloat price),
                        RightAlignCell(HumanReadableFloat mrAll)
                    )

            let sell =
                proc.Value.GetTotalProductPrice(PriceFetchMode.Sell)

            let sellWithTax =
                proc.Value.GetTotalProductPrice(PriceFetchMode.SellWithTax)

            tt.AddRow(
                "卖出/税后",
                RightAlignCell "--",
                RightAlignCell(HumanReadableFloat sell),
                RightAlignCell(HumanReadableFloat sellWithTax)
            )

            tt.AddRow(
                "材料/最佳",
                RightAlignCell "--",
                RightAlignCell(HumanReadableFloat allCost),
                RightAlignCell(HumanReadableFloat optCost)
            )

            tt.AddRow(
                "税后 利润",
                RightAlignCell "--",
                RightAlignCell(HumanReadableFloat(sellWithTax - allCost)),
                RightAlignCell(HumanReadableFloat(sellWithTax - optCost))
            )

            using (cmdArg.OpenResponse(cfg.IsImageOutput)) (fun ret -> ret.Write(tt))

    [<CommandHandlerMethodAttribute("EVE舰船II", "T2舰船制造总览", "")>]
    [<CommandHandlerMethodAttribute("EVE舰船", "T2舰船制造总览", "")>]
    [<CommandHandlerMethodAttribute("EVE组件", "T2和旗舰组件制造总览", "")>]
    [<CommandHandlerMethodAttribute("EVE种菜", "EVE种菜利润", "")>]
    [<CommandHandlerMethodAttribute("EVE装备II", "EVET2装备利润", "需要关键词")>]
    member x.HandleManufacturingOverview(cmdArg : CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.RegisterOption("by", "")
        cfg.Parse(cmdArg.Arguments)

        use ret = cmdArg.OpenResponse(cfg.IsImageOutput)
        ret.WriteLine(ToolWarning)

        ret.WriteLine(
            sprintf
                "输入效率：%i%% 默认效率：%i%% 成本指数：%i%% 设施税率%i%%"
                cfg.InputMe
                cfg.DerivativetMe
                cfg.SystemCostIndex
                cfg.StructureTax
        )

        ret.WriteLine(sprintf "展开行星材料：%b 展开反应公式：%b" cfg.ExpandPlanet cfg.ExpandReaction)

        let filterFunc : (EveProcess -> bool) =
            match cmdArg.CommandName with // 注意小写匹配
            | "eve组件" ->
                fun bp ->
                    (bp.Type = ProcessType.Manufacturing)
                    && ((bp.Process.GetFirstProduct().Item.GroupId = 334) // Tech2ComponentGroupId
                        || (bp.Process.GetFirstProduct().Item.GroupId = 873)) // CapitalComponentGroupId
            | "eve种菜" -> fun bp -> (bp.Type = ProcessType.Planet)
            | "eve舰船" ->
                fun bp ->
                    (bp.Type = ProcessType.Manufacturing)
                    && (bp.Process.GetFirstProduct().Item.CategoryId = 6) // 6 = 舰船
                    && (bp.Process.GetFirstProduct().Item.MetaGroupId <> 2) // T1
                    && (let mg =
                            bp.Process.GetFirstProduct().Item.MarketGroup

                        mg.IsSome && (not <| mg.Value.Name.Contains("特别")))
            | "eve舰船ii" ->
                fun bp ->
                    (bp.Type = ProcessType.Manufacturing)
                    && (bp.Process.GetFirstProduct().Item.CategoryId = 6) // 6 = 舰船
                    && (bp.Process.GetFirstProduct().Item.MetaGroupId = 2) // T2
                    && (let mg =
                            bp.Process.GetFirstProduct().Item.MarketGroup

                        mg.IsSome && (not <| mg.Value.Name.Contains("特别")))
            | "eve装备ii" ->
                let isGroup = cfg.GetValue("by") = "group"

                if isGroup then ret.WriteLine("按组名匹配") else ret.WriteLine("按名称匹配，按组名匹配请使用by:group")

                let keyword =
                    if cfg.CommandLine.Length = 0 then ret.AbortExecution(InputError, "需要一个装备名称关键词")

                    cfg.CommandLine.[0]

                if keyword.Length < 2 then ret.AbortExecution(InputError, "至少2个字")

                if keyword.Contains("I") then ret.AbortExecution(InputError, "emmm 想看全部T2还是别想了")

                let allowCategoryId = [| 7; 18; 8 |] |> Set // 装备，无人机，弹药

                fun bp ->
                    (bp.Type = ProcessType.Manufacturing)
                    && (if isGroup then
                            bp
                                .Process
                                .GetFirstProduct()
                                .Item.TypeGroup.Name.Contains(keyword)
                        else
                            bp
                                .Process
                                .GetFirstProduct()
                                .Item.Name.Contains(keyword))
                    && (allowCategoryId.Contains(bp.Process.GetFirstProduct().Item.CategoryId)) // 装备
                    && (bp.Process.GetFirstProduct().Item.MetaGroupId = 2) // T2

            | other -> cmdArg.AbortExecution(ModuleError, "不应发生匹配:{0}", other)


        let pmStr = cfg.MaterialPriceMode.ToString()

        let pm = EveProcessManager(cfg)

        BlueprintCollection.Instance.GetAllProcesses()
        |> Seq.filter filterFunc
        |> (fun seq ->
            if (Seq.length seq) = 0 then ret.AbortExecution(InputError, "无符合要求的蓝图信息")

            seq)
        |> Seq.map
            (fun ps ->
                let proc = pm.ApplyProcess(ps, ByRun 1.0)
                let product = proc.Process.GetFirstProduct()

                let cost =
                    proc.GetTotalMaterialPrice(cfg.MaterialPriceMode)
                    + proc.GetInstallationCost(cfg)

                let sellWithTax =
                    ps.GetTotalProductPrice(PriceFetchMode.SellWithTax)

                let volume = data.GetItemTradeVolume(product.Item)

                {| Name = product.Item.Name
                   TypeGroup = product.Item.TypeGroup
                   Cost = cost
                   Quantity = product.Quantity
                   Sell = ps.GetTotalProductPrice(PriceFetchMode.Sell)
                   Profit = sellWithTax - cost
                   Volume = volume |})
        |> Seq.sortByDescending (fun x -> x.Profit)
        |> Seq.groupBy (fun x -> x.TypeGroup)
        |> Seq.iter
            (fun (group, data) ->
                ret.WriteLine(">>{0}<<", group.Name)

                let tt =
                    TextTable(
                        "方案",
                        RightAlignCell "出售价格/无税卖出",
                        RightAlignCell("生产成本/" + pmStr),
                        RightAlignCell "含税利润",
                        RightAlignCell "日均交易"
                    )

                for x in data do
                    tt.AddRow(
                        x.Name,
                        x.Sell |> HumanReadableFloat |> RightAlignCell,
                        x.Cost |> HumanReadableFloat |> RightAlignCell,
                        x.Profit |> HumanReadableFloat |> RightAlignCell,
                        x.Volume |> HumanReadableFloat |> RightAlignCell
                    )

                ret.Write(tt)
                ret.WriteEmptyLine())
