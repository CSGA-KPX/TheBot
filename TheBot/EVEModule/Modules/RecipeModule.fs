namespace KPX.TheBot.Module.EveModule

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

open KPX.TheBot.Data.CommonModule.Recipe
open KPX.TheBot.Data.EveData.Utils
open KPX.TheBot.Data.EveData.EveType
open KPX.TheBot.Data.EveData.Process

open KPX.TheBot.Utils.RecipeRPN

open KPX.TheBot.Module.EveModule.Utils.Helpers
open KPX.TheBot.Module.EveModule.Utils.Config
open KPX.TheBot.Module.EveModule.Utils.Data
open KPX.TheBot.Module.EveModule.Utils.Extensions


type EveRecipeModule() =
    inherit CommandHandlerBase()

    let data = DataBundle.Instance
    let pm = EveProcessManager.Default

    let er = EveExpression.EveExpression()

    [<CommandHandlerMethodAttribute("#eme", "EVE蓝图材料效率计算", "")>]
    member x.HandleME(cmdArg : CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.Arguments)

        let item = data.TryGetItem(cfg.CmdLineAsString)

        if item.IsNone
        then cmdArg.AbortExecution(InputError, "找不到物品：{0}", cfg.CmdLineAsString)

        let recipe =
            pm.TryGetRecipe(item.Value, ByRun 1.0, 0)

        if recipe.IsNone
        then cmdArg.AbortExecution(InputError, "找不到蓝图：{0}", cfg.CmdLineAsString)

        let me0Price =
            recipe.Value.GetTotalMaterialPrice(PriceFetchMode.Sell, MeApplied)

        let tt =
            TextTable(RightAlignCell "材料等级", RightAlignCell "节省")

        tt.AddPreTable(
            "直接材料总价："
            + System.String.Format("{0:N0}", ceil me0Price)
        )

        for me = 0 to 10 do
            let cost =
                pm
                    .TryGetRecipe(item.Value, ByRun 1.0, me)
                    .Value.GetTotalMaterialPrice(PriceFetchMode.Sell, MeApplied)

            let save = me0Price - cost |> ceil
            tt.AddRow(me, HumanReadableInteger save)

        using (cmdArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("#er",
                                    "EVE蓝图材料计算",
                                    "可以使用表达式，多个物品需用+连接。可选参数见#evehelp。如：
#r 帝国海军散热槽*10+机器人技术*9999")>]
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
                let recipeOpt =
                    pm.TryGetRecipe(mr.Item, ByRun mr.Quantity)

                match recipeOpt with
                | Some recipe when mr.Quantity > 0.0 ->
                    let proc = recipe.ApplyFlags(MeApplied)
                    let product = proc.GetFirstProduct()
                    tt.AddRow("产出：" + product.Item.Name, HumanReadableInteger product.Quantity)

                    for m in proc.Input do
                        final.Update(m)
                | _ when mr.Quantity < 0.0 ->
                    // 已有材料需要扣除
                    final.Update(mr)
                | _ ->
                    cmdArg.AbortExecution(
                        ModuleError,
                        "不知道如何处理：{0} * {1}",
                        mr.Item.Name,
                        mr.Quantity
                    )

        for mr in final do
            tt.AddRow(mr.Item.Name, HumanReadableInteger mr.Quantity)

        using (cmdArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("#err",
                                    "EVE蓝图基础材料计算",
                                    "可以使用表达式，多个物品需用+连接。可选参数见#evehelp。如：
#rr 帝国海军散热槽*10+机器人技术*9999")>]
    member x.HandleRR(cmdArg : CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.Arguments)

        let final = ItemAccumulator<EveType>()
        let tt = TextTable("名称", RightAlignCell "数量")
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
                let product = finalProc.GetFirstProduct()

                tt.AddRow("产出：" + product.Item.Name, HumanReadableInteger product.Quantity)

                for m in finalProc.Input do
                    final.Update(m)

        for mr in final do
            tt.AddRow(mr.Item.Name, HumanReadableInteger mr.Quantity)

        using (cmdArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("#errc", "EVE蓝图成本计算", "不支持表达式，但仅限一个物品。可选参数见#evehelp。如：
#errc 帝国海军散热槽*10")>]
    member x.HandleERRCV2(cmdArg : CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.Arguments)

        match er.Eval(cfg.CmdLineAsString) with
        | Number n -> cmdArg.AbortExecution(InputError, "结算结果为数字: {0}", n)
        | Accumulator a ->
            let mr = a |> Seq.tryHead

            if mr.IsNone then cmdArg.AbortExecution(InputError, "没有可供计算的物品")

            let pm = EveProcessManager(cfg)

            let recipe =
                pm.TryGetRecipe(mr.Value.Item, ByRun mr.Value.Quantity)

            if recipe.IsNone
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

            let proc = recipe.Value.ApplyFlags(MeApplied)
            let product = proc.GetFirstProduct()
            priceTable.AddObject(product.Item, product.Quantity)
            tt.AddPreTable(priceTable)

            tt.AddPreTable("材料：")

            let installFee = recipe.Value.GetInstallationCost(cfg)
            tt.AddRow("制造费用", PaddingRight, HumanReadableSig4Float installFee, PaddingRight)

            let mutable optCost = installFee
            let mutable allCost = installFee

            for mr in proc.Input do
                let price = // 市场价格
                    mr.Item.GetPrice(cfg.MaterialPriceMode)
                    * mr.Quantity

                let mrProc =
                    pm.TryGetRecipeRecMe(
                        mr.Item,
                        ByItem mr.Quantity,
                        cfg.DerivativetMe,
                        cfg.DerivativetMe
                    )

                if mrProc.IsSome
                   && pm.CanExpand(mrProc.Value.InputProcess) then
                    let mrInstall =
                        mrProc.Value.IntermediateProcess
                        |> Array.fold (fun acc proc -> acc + proc.GetInstallationCost(cfg)) 0.0

                    let mrCost =
                        mrProc.Value.FinalProcess.Input.GetPrice(cfg.MaterialPriceMode)

                    let mrAll = mrInstall + mrCost
                    allCost <- allCost + mrAll

                    optCost <-
                        optCost
                        + (if (mrAll >= price) && (price <> 0.0) then price else mrAll)

                    tt.AddRow(
                        mr.Item.Name,
                        HumanReadableInteger mr.Quantity,
                        HumanReadableSig4Float price,
                        HumanReadableSig4Float mrAll
                    )
                else
                    optCost <- optCost + price
                    allCost <- allCost + price

                    tt.AddRow(
                        mr.Item.Name,
                        HumanReadableInteger mr.Quantity,
                        HumanReadableSig4Float price,
                        PaddingRight
                    )


            let sell =
                proc.Output.GetPrice(PriceFetchMode.Sell)

            let sellWithTax =
                proc.Output.GetPrice(PriceFetchMode.SellWithTax)

            tt.AddRow(
                "卖出/税后",
                PaddingRight,
                HumanReadableSig4Float sell,
                HumanReadableSig4Float sellWithTax
            )

            tt.AddRow(
                "材料/最佳",
                PaddingRight,
                HumanReadableSig4Float allCost,
                HumanReadableSig4Float optCost
            )

            tt.AddRow(
                "税后 利润",
                PaddingRight,
                HumanReadableSig4Float(sellWithTax - allCost),
                HumanReadableSig4Float(sellWithTax - optCost)
            )

            using (cmdArg.OpenResponse(cfg.IsImageOutput)) (fun ret -> ret.Write(tt))

    [<CommandHandlerMethodAttribute("#EVE舰船II", "T2舰船制造总览", "可选参数见#evehelp。")>]
    [<CommandHandlerMethodAttribute("#EVE舰船", "T1舰船制造总览", "可选参数见#evehelp。")>]
    [<CommandHandlerMethodAttribute("#EVE组件", "T2和旗舰组件制造总览", "可选参数见#evehelp。")>]
    [<CommandHandlerMethodAttribute("#EVE种菜", "EVE种菜利润", "可选参数见#evehelp。")>]
    [<CommandHandlerMethodAttribute("#EVE装备II", "EVET2装备利润", "可以使用by:搜索物品组名称。其他可选参数见#evehelp。")>]
    [<CommandHandlerMethodAttribute("#EVE燃料块", "EVE燃料块", "可选参数见#evehelp。")>]
    [<CommandHandlerMethodAttribute("#EVE生产", "EVE生产总览", "可选参数：
mgid 匹配metaGroupId
cid 匹配categoryId
gid 匹配groupId
n 匹配物品名称
gn 匹配组名称
mgn 匹配metagroup名称
no 排除物品名称
gno 排除组名称
mgno 排除metagroup名称
其他可选参数见#evehelp。")>]
    member x.HandleManufacturingOverview(cmdArg : CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.RegisterOption("by", "")
        // 生产总览使用
        cfg.RegisterOption("mgid", "1,2,4,14")
        cfg.RegisterOption("cid", "")
        cfg.RegisterOption("gid", "")
        cfg.RegisterOption("n", "")
        cfg.RegisterOption("gn", "")
        cfg.RegisterOption("mgn", "")
        cfg.RegisterOption("no", "")
        cfg.RegisterOption("gno", "")
        cfg.RegisterOption("mgno", "")

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

        let searchCond =
            match cmdArg.CommandName with
            | "#EVE燃料块" -> PredefinedSearchCond.FuelBlocks
            | "#EVE种菜" ->
                ret.WriteLine("海关税率10%，按进出口计税。推荐使用p:展开材料到P1。")
                PredefinedSearchCond.Planet
            | "#EVE组件" -> PredefinedSearchCond.Components
            | "#EVE舰船" -> PredefinedSearchCond.T1Ships
            | "#EVE舰船II" -> PredefinedSearchCond.T2Ships
            | "#EVE装备II" ->
                let isGroup = cfg.GetValue("by") = "group"

                let keyword =
                    if cfg.CommandLine.Length = 0 then ret.AbortExecution(InputError, "需要一个装备名称关键词")

                    cfg.CommandLine.[0]

                let cond =
                    if isGroup then ByGroupName keyword else ByItemName keyword

                PredefinedSearchCond.T2ModulesOf(cond)
            | "#EVE生产" ->
                let cond =
                    ProcessSearchCond(ProcessType.Manufacturing)

                let mgid =
                    cfg
                        .GetValue("mgid")
                        .Split(',', System.StringSplitOptions.RemoveEmptyEntries)

                if mgid.Length <> 0 then cond.MetaGroupIds <- mgid |> Array.map int

                let gid =
                    cfg
                        .GetValue("gid")
                        .Split(',', System.StringSplitOptions.RemoveEmptyEntries)

                if gid.Length <> 0 then cond.GroupIds <- gid |> Array.map int

                let cid =
                    cfg
                        .GetValue("cid")
                        .Split(',', System.StringSplitOptions.RemoveEmptyEntries)

                if cid.Length <> 0 then cond.CategoryIds <- cid |> Array.map int

                let nameSearch = ResizeArray<NameSearchCond>()

                let name = cfg.GetValue("n")
                if name <> "" then nameSearch.Add(ByItemName name)

                let gName = cfg.GetValue("gn")
                if gName <> "" then nameSearch.Add(ByGroupName gName)

                let mgName = cfg.GetValue("mgn")

                if mgName <> "" then nameSearch.Add(ByMarketGroupName mgName)

                cond.NameSearch <- nameSearch.ToArray()
                nameSearch.Clear()

                let name = cfg.GetValue("no")
                if name <> "" then nameSearch.Add(ByItemName name)

                let gName = cfg.GetValue("gno")
                if gName <> "" then nameSearch.Add(ByGroupName gName)

                let mgName = cfg.GetValue("mgno")

                if mgName <> "" then nameSearch.Add(ByMarketGroupName mgName)

                cond.NameExclude <- nameSearch.ToArray()

                cond
            | other -> cmdArg.AbortExecution(ModuleError, "不应发生匹配:{0}", other)

        let pmStr = cfg.MaterialPriceMode.ToString()

        let pm = EveProcessManager(cfg)

        // 正式开始以前写一个空行
        ret.WriteEmptyLine()

        match EveProcessSearch.Instance.Search(searchCond) with
        | NoResult -> ret.AbortExecution(InputError, "无符合要求的蓝图信息")
        | TooManyResults -> ret.AbortExecution(InputError, "蓝图数量超限")
        | Result result ->
            result
            |> Seq.map
                (fun ps ->
                    let product = ps.Original.GetFirstProduct()

                    let proc =
                        pm
                            .TryGetRecipeRecMe(product.Item,
                                               ByRun 1.0,
                                               cfg.InputMe,
                                               cfg.DerivativetMe)
                            .Value

                    // 所有基础材料的报价
                    let materialCost =
                        proc.FinalProcess.Input.GetPrice(cfg.MaterialPriceMode)

                    let installCost =
                        if ps.Type = ProcessType.Planet then
                            // 构造一个临时配方去计算费用
                            { Original = proc.FinalProcess
                              TargetQuantity = ByRun 1.0
                              TargetMe = 0
                              Type = ProcessType.Planet }
                                .GetInstallationCost(cfg)
                        else
                            proc.IntermediateProcess
                            |> Array.fold (fun acc proc -> acc + proc.GetInstallationCost(cfg)) 0.0

                    let cost = materialCost + installCost

                    let sellWithTax =
                        proc.FinalProcess.Output.GetPrice(PriceFetchMode.SellWithTax)

                    let volume = data.GetItemTradeVolume(product.Item)

                    let sortIdx =
                        //(sellWithTax - cost) / cost * 100.0 |> int
                       (sellWithTax - cost) * volume /// 利润*平均交易量

                    {| Name = product.Item.Name
                       TypeGroup = product.Item.TypeGroup
                       Cost = cost
                       Quantity = product.Quantity
                       Sell = proc.FinalProcess.Output.GetPrice(PriceFetchMode.Sell)
                       Profit = sellWithTax - cost
                       Volume = volume
                       SortIndex = sortIdx |})
            |> Seq.sortByDescending (fun x -> x.SortIndex)
            |> Seq.groupBy (fun x -> x.TypeGroup)
            |> Seq.iter
                (fun (group, data) ->
                    ret.WriteLine(">>{0}<<", group.Name)

                    let tt =
                        TextTable(
                            "方案",
                            RightAlignCell "出售价格/税前卖出",
                            RightAlignCell("生产成本/" + pmStr),
                            RightAlignCell "含税利润",
                            RightAlignCell "交易量",
                            RightAlignCell "日均利润"
                        )

                    for x in data do
                        tt.AddRow(
                            x.Name,
                            HumanReadableSig4Float x.Sell,
                            HumanReadableSig4Float x.Cost,
                            HumanReadableSig4Float x.Profit,
                            HumanReadableInteger x.Volume,
                            HumanReadableSig4Float x.SortIndex
                        )

                    ret.Write(tt)
                    ret.WriteEmptyLine())
