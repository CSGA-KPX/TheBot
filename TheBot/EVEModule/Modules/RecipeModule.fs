namespace KPX.TheBot.Module.EveModule

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Testing
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable
open KPX.FsCqHttp.Utils.UserOption

open KPX.TheBot.Data.CommonModule.Recipe
open KPX.TheBot.Data.EveData.Utils
open KPX.TheBot.Data.EveData.EveType
open KPX.TheBot.Data.EveData.Process

open KPX.TheBot.Utils.RecipeRPN

open KPX.TheBot.Module.EveModule.Utils.Helpers
open KPX.TheBot.Module.EveModule.Utils.Config
open KPX.TheBot.Module.EveModule.Utils.Data
open KPX.TheBot.Module.EveModule.Utils.Extensions
open KPX.TheBot.Module.EveModule.Utils.UserInventory


type EveRecipeModule() =
    inherit CommandHandlerBase()

    let data = DataBundle.Instance
    let pm = EveProcessManager.Default

    let er = EveExpression.EveExpression()

    let ic = InventoryCollection.Instance

    [<CommandHandlerMethodAttribute("#eme", "EVE蓝图材料效率计算", "")>]
    member x.HandleME(cmdArg : CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.Arguments)


        let item =
            data.TryGetItem(cfg.GetNonOptionString())

        if item.IsNone then
            cmdArg.Abort(InputError, "找不到物品：{0}", cfg.GetNonOptionString())

        let recipe =
            pm.TryGetRecipe(item.Value, ByRun 1.0, 0)

        if recipe.IsNone then
            cmdArg.Abort(InputError, "找不到蓝图：{0}", cfg.GetNonOptionString())

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

        using (cmdArg.OpenResponse(cfg.ResponseType)) (fun x -> x.Write(tt))

    [<TestFixture>]
    member x.TestME() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#eme 恶狼级蓝图")
        tc.ShouldNotThrow("#eme 恶狼级")
        tc.ShouldThrow("#eme")
        tc.ShouldThrow("#eme 军用馒头蓝图")
        tc.ShouldThrow("#eme 军用馒头")

    [<CommandHandlerMethodAttribute("#er",
                                    "EVE蓝图材料计算",
                                    "可以使用表达式，多个物品需用+连接。可选参数见#evehelp。如：
#r 帝国海军散热槽*10+机器人技术*9999")>]
    [<CommandHandlerMethodAttribute("#err",
                                    "EVE蓝图基础材料计算",
                                    "可以使用表达式，多个物品需用+连接。可选参数见#evehelp。如：
#rr 帝国海军散热槽*10+机器人技术*9999")>]
    member x.HandleRRR(cmdArg : CommandEventArgs) =
        let cfg = EveConfigParser()
        // 默认值必须是不可能存在的值，比如空格
        let idOpt = cfg.RegisterOption<string>("id", "\r\n")
        cfg.Parse(cmdArg.Arguments)

        let tt =
            if idOpt.IsDefined then
                TextTable("名称", RightAlignCell "数量", RightAlignCell "需求", RightAlignCell "总体积")
            else
                TextTable("名称", RightAlignCell "数量", RightAlignCell "体积")

        let inv =
            match idOpt.IsDefined with
            | false -> ItemAccumulator<EveType>()
            | true when ic.Contains(idOpt.Value) ->
                tt.AddPreTable($"已经扣除指定材料表中已有材料")
                snd (ic.TryGet(idOpt.Value).Value)
            | true -> cmdArg.Abort(InputError, "没有和id关联的材料表")

        let isR = cmdArg.CommandAttrib.Command = "#er"

        if isR then
            tt.AddPreTable(sprintf "输入效率：%i%% " cfg.InputMe)
        else
            tt.AddPreTable(sprintf "输入效率：%i%% 默认效率：%i%%" cfg.InputMe cfg.DerivativetMe)
            tt.AddPreTable(sprintf "展开行星材料：%b 展开反应公式：%b" cfg.ExpandPlanet cfg.ExpandReaction)

        let final = ItemAccumulator<EveType>()
        let mutable totalInputVolume = 0.0
        let mutable totalOutputVolume = 0.0

        match er.Eval(cfg.GetNonOptionString()) with
        | Number n -> cmdArg.Abort(InputError, "结算结果为数字: {0}", n)
        | Accumulator a ->
            let pm = EveProcessManager(cfg)

            for mr in a do
                let proc =
                    if isR then
                        pm.TryGetRecipe(mr.Item, ByRun mr.Quantity)
                        |> Option.map (fun ret -> ret.ApplyFlags(MeApplied))
                    else
                        pm.TryGetRecipeRecMe(mr.Item, ByRun mr.Quantity)
                        |> Option.map (fun ret -> ret.FinalProcess)

                if proc.IsNone then
                    cmdArg.Abort(InputError, "找不到配方：{0}", mr.Item.Name)

                let product = proc.Value.GetFirstProduct()

                let outputVolume = product.Item.Volume * product.Quantity
                totalOutputVolume <- totalOutputVolume + outputVolume

                tt.RowBuilder {
                    yield "产出：" + product.Item.Name
                    yield HumanReadableInteger product.Quantity
                    if idOpt.IsDefined then yield PaddingRight
                    yield HumanReadableInteger outputVolume
                }
                |> tt.AddRow

                for m in proc.Value.Input do
                    final.Update(m)

        tt.RowBuilder {
            yield "产出：总体积"
            yield PaddingRight
            if idOpt.IsDefined then yield PaddingRight
            yield HumanReadableInteger totalOutputVolume
        }
        |> tt.AddRow

        tt.RowBuilder {
            yield PaddingLeft
            yield PaddingRight
            if idOpt.IsDefined then yield PaddingRight
            yield PaddingRight
        }
        |> tt.AddRow

        for mr in final
                  |> Seq.sortBy (fun x -> x.Item.MarketGroupId) do
            let sumVolume = mr.Item.Volume * mr.Quantity

            let need =
                if inv.Contains(mr.Item) then
                    mr.Quantity - inv.Get(mr.Item)
                else
                    mr.Quantity

            let needStr = 
                if need <= 0.0 then PaddingRight
                else HumanReadableInteger (need)

            tt.RowBuilder {
                yield mr.Item.Name
                yield HumanReadableInteger mr.Quantity
                if idOpt.IsDefined then yield needStr
                yield HumanReadableInteger sumVolume
            }
            |> tt.AddRow

            totalInputVolume <- totalInputVolume + sumVolume

        tt.RowBuilder {
            yield "材料体积"
            yield PaddingRight
            if idOpt.IsDefined then yield PaddingRight
            yield HumanReadableInteger totalInputVolume
        }
        |> tt.AddRow

        using (cmdArg.OpenResponse(cfg.ResponseType)) (fun x -> x.Write(tt))

    [<TestFixture>]
    member x.TestRRR() =
        let tc = TestContext(x)
        tc.ShouldThrow("#er")
        tc.ShouldThrow("#er 5*5")
        tc.ShouldThrow("#er 军用馒头 ime:10")
        tc.ShouldThrow("#er 军用馒头蓝图")

        tc.ShouldNotThrow("#er 恶狼级")
        tc.ShouldNotThrow("#er 恶狼级蓝图 ime:10")

        tc.ShouldThrow("#err")
        tc.ShouldThrow("#err 5*5")
        tc.ShouldThrow("#err 军用馒头 ime:10")
        tc.ShouldThrow("#err 军用馒头蓝图")

        tc.ShouldNotThrow("#err 恶狼级")
        tc.ShouldNotThrow("#err 恶狼级蓝图 ime:10")

    [<CommandHandlerMethodAttribute("#errc",
                                    "EVE蓝图成本计算",
                                    "不支持表达式，但仅限一个物品。可选参数见#evehelp。如：
#errc 帝国海军散热槽*10")>]
    member x.HandleERRCV2(cmdArg : CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.Arguments)

        match er.Eval(cfg.GetNonOptionString()) with
        | Number n -> cmdArg.Abort(InputError, "结算结果为数字: {0}", n)
        | Accumulator a ->
            if a.Count > 1 then
                cmdArg.Abort(InputError, "#errc只允许计算一个物品")

            let mr = a |> Seq.tryHead

            if mr.IsNone then cmdArg.Abort(InputError, "没有可供计算的物品")

            match mr.Value.Item.MetaGroupId with
            | 1
            | 54 -> 10 // T1装备建筑默认10
            | 2
            | 14
            | 53 -> 2 // T2/T3装备 建筑默认2
            | _ -> 0 // 其他默认0
            // TODO : 这个功能应该汇入上级，可以供dme使用
            |> cfg.SetDefaultInputMe

            let pm = EveProcessManager(cfg)

            let recipe =
                pm.TryGetRecipe(mr.Value.Item, ByRun mr.Value.Quantity)

            if recipe.IsNone then
                cmdArg.Abort(InputError, "找不到配方:{0}", mr.Value.Item.Name)

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

            for mr in proc.Input
                      |> Seq.sortBy (fun x -> x.Item.MarketGroupId) do
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

                if
                    mrProc.IsSome
                    && pm.CanExpand(mrProc.Value.InputProcess)
                then
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

            using (cmdArg.OpenResponse(cfg.ResponseType)) (fun ret -> ret.Write(tt))

    [<TestFixture>]
    member x.TestERRC() =
        let tc = TestContext(x)
        tc.ShouldThrow("#errc")
        tc.ShouldThrow("#errc 5*5")
        tc.ShouldThrow("#errc 军用馒头 ime:10")
        tc.ShouldThrow("#errc 军用馒头蓝图")

        tc.ShouldNotThrow("#errc 恶狼级")
        tc.ShouldNotThrow("#errc 恶狼级蓝图 ime:10")

    [<CommandHandlerMethodAttribute("#EVE舰船II", "T2舰船制造总览", "可选参数见#evehelp。")>]
    [<CommandHandlerMethodAttribute("#EVE舰船", "T1舰船制造总览", "可选参数见#evehelp。")>]
    [<CommandHandlerMethodAttribute("#EVE组件", "T2和旗舰组件制造总览", "可选参数见#evehelp。")>]
    [<CommandHandlerMethodAttribute("#EVE种菜", "EVE种菜利润", "可选参数见#evehelp。")>]
    [<CommandHandlerMethodAttribute("#EVE装备II", "EVET2装备利润", "可以使用by:搜索物品组名称。其他可选参数见#evehelp。")>]
    [<CommandHandlerMethodAttribute("#EVE燃料块", "EVE燃料块", "可选参数见#evehelp。")>]
    member x.HandleManufacturingOverview(cmdArg : CommandEventArgs) =
        let cfg = EveConfigParser()

        let searchByGroupName =
            cfg.RegisterOption(
                { new OptionCell<bool>(cfg, "by", false) with
                    override x.ConvertValue(opt) = opt = "group" }
            )

        cfg.Parse(cmdArg.Arguments)

        use ret = cmdArg.OpenResponse(cfg.ResponseType)
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
                let keyword =
                    if cfg.NonOptionStrings.Count = 0 then
                        ret.Abort(InputError, "需要一个装备名称关键词")

                    cfg.NonOptionStrings.[0]

                let cond =
                    if searchByGroupName.Value then
                        ByGroupName keyword
                    else
                        ByItemName keyword

                PredefinedSearchCond.T2ModulesOf(cond)
            | other -> cmdArg.Abort(ModuleError, "不应发生匹配:{0}", other)

        let pmStr = cfg.MaterialPriceMode.ToString()

        let pm = EveProcessManager(cfg)

        // 正式开始以前写一个空行
        ret.WriteEmptyLine()

        match EveProcessSearch.Instance.Search(searchCond) with
        | NoResult -> ret.Abort(InputError, "无符合要求的蓝图信息")
        | TooManyResults -> ret.Abort(InputError, "蓝图数量超限")
        | Result result ->
            result
            |> Seq.map
                (fun ps ->
                    let product = ps.Original.GetFirstProduct()

                    let proc =
                        pm
                            .TryGetRecipeRecMe(
                                product.Item,
                                ByRun 1.0,
                                cfg.InputMe,
                                cfg.DerivativetMe
                            )
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
                        (sellWithTax - cost) * volume
                        / proc.FinalProcess.Output.[0].Quantity

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

    [<TestFixture>]
    member x.TestManufacturingOverview() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#EVE燃料块")
        tc.ShouldNotThrow("#EVE燃料块 ime:10")
        tc.ShouldNotThrow("#EVE燃料块 ime:10 sci:10")
        tc.ShouldNotThrow("#EVE装备Ii by:group 气云")
        tc.ShouldNotThrow("#EVE装备Ii 气云")
