namespace KPX.EvePlugin.Modules

open System
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Testing
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.UserOption

open KPX.TheBot.Host.DataModel.Recipe
open KPX.TheBot.Host.Utils.RecipeRPN

open KPX.EvePlugin.Data.Utils
open KPX.EvePlugin.Data.EveType
open KPX.EvePlugin.Data.Process

open KPX.EvePlugin.Utils
open KPX.EvePlugin.Utils.Helpers
open KPX.EvePlugin.Utils.Config
open KPX.EvePlugin.Utils.Data
open KPX.EvePlugin.Utils.Extensions
open KPX.EvePlugin.Utils.UserInventory


type EveRecipeModule() =
    inherit CommandHandlerBase()

    let data = DataBundle.Instance
    let pm = EveProcessManager.Default

    let er = EveExpression.EveExpression()

    let ic = InventoryCollection.Instance

    [<CommandHandlerMethod("#eme", "EVE蓝图材料效率计算", "")>]
    member x.HandleME(cmdArg: CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.HeaderArgs)

        let item = data.TryGetItem(cfg.GetNonOptionString())

        if item.IsNone then
            cmdArg.Abort(InputError, "找不到物品：{0}", cfg.GetNonOptionString())

        let recipe = pm.TryGetRecipe(item.Value, ByRun 1.0, 0)

        if recipe.IsNone then
            cmdArg.Abort(InputError, "找不到蓝图：{0}", cfg.GetNonOptionString())

        let me0Price = recipe.Value.GetTotalMaterialPrice(PriceFetchMode.Sell, MeApplied)

        TextTable(cfg.ResponseType) {
            CellBuilder() {
                literal "直接材料总价："
                integer me0Price
            }

            [ CellBuilder() { rightLiteral "材料等级" }; CellBuilder() { rightLiteral "节省" } ]

            [ for me = 0 to 10 do
                  let cost =
                      pm
                          .TryGetRecipe(item.Value, ByRun 1.0, me)
                          .Value.GetTotalMaterialPrice(PriceFetchMode.Sell, MeApplied)

                  [ CellBuilder() { integer me }; CellBuilder() { integer (me0Price - cost) } ] ]
        }


    [<TestFixture>]
    member x.TestME() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#eme 恶狼级蓝图")
        tc.ShouldNotThrow("#eme 恶狼级")
        tc.ShouldThrow("#eme")
        tc.ShouldThrow("#eme 军用馒头蓝图")
        tc.ShouldThrow("#eme 军用馒头")

    [<CommandHandlerMethod("#er",
                           "EVE蓝图材料计算",
                           "可以使用表达式，多个物品需用+连接。可选参数见#evehelp。如：
#r 帝国海军散热槽*10+机器人技术*9999")>]
    [<CommandHandlerMethod("#err",
                           "EVE蓝图基础材料计算",
                           "可以使用表达式，多个物品需用+连接。可选参数见#evehelp。如：
#rr 帝国海军散热槽*10+机器人技术*9999")>]
    member x.HandleRRR(cmdArg: CommandEventArgs) =
        let cfg = EveConfigParser()
        // 默认值必须是不可能存在的值，比如空格
        let idOpt = cfg.RegisterOption<string>("id", "\r\n")
        cfg.Parse(cmdArg.HeaderArgs)

        let inv =
            match idOpt.IsDefined with
            | false -> ItemAccumulator<EveType>()
            | true when ic.Contains(idOpt.Value) -> snd (ic.TryGet(idOpt.Value).Value)
            | true -> cmdArg.Abort(InputError, "没有和id关联的材料表")

        let isR = cmdArg.CommandAttrib.Command = "#er"
        let useInv = idOpt.IsDefined
        let respType = cfg.ResponseType // 后面会被重写

        let inputAcc = ItemAccumulator<EveType>()
        let outputAcc = ItemAccumulator<EveType>()
        let mutable totalInputVolume = 0.0
        let mutable totalOutputVolume = 0.0

        let productTable =
            TextTable() {
                [ CellBuilder() { literal "产出" }
                  CellBuilder() { rightLiteral "数量" }
                  CellBuilder() { rightLiteral "体积" }
                  CellBuilder() { rightLiteral "ime" }
                  CellBuilder() { rightLiteral "dme" }
                  CellBuilder() { rightLiteral "r" }
                  CellBuilder() { rightLiteral "p" } ]
            }

        for args in cmdArg.AllArgs do
            cfg.Parse(args)
            let str = cfg.GetNonOptionString()

            if not <| String.IsNullOrWhiteSpace(str) then
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
                        outputAcc.Update(product)

                        let outputVolume = product.Item.Volume * product.Quantity
                        totalOutputVolume <- totalOutputVolume + outputVolume

                        productTable {
                            [ CellBuilder() { literal product.Item.Name }
                              CellBuilder() { integer product.Quantity }
                              CellBuilder() { integer outputVolume }
                              CellBuilder() { integer cfg.InputMe }
                              CellBuilder() { integer cfg.DerivationMe }
                              CellBuilder() { literal cfg.ExpandReaction }
                              CellBuilder() { literal cfg.ExpandPlanet } ]
                        }
                        |> ignore

                        for m in proc.Value.Input do
                            inputAcc.Update(m)

        // 生成产物信息
        productTable {
            [ CellBuilder() { literal "总计" }
              CellBuilder() { rightPad }
              CellBuilder() { number totalOutputVolume }
              CellBuilder() { rightPad }
              CellBuilder() { rightPad }
              CellBuilder() { rightPad }
              CellBuilder() { rightPad } ]
        }
        |> ignore

        let mainTable =
            TextTable(respType) {
                productTable

                [ CellBuilder() { literal "名称" }
                  CellBuilder() { rightLiteral "数量" }
                  CellBuilder() { rightLiteral "缺少" }
                  CellBuilder() { rightLiteral "体积" } ]
            }

        // 生成材料信息
        for mr in inputAcc |> Seq.sortBy (fun x -> x.Item.MarketGroupId) do
            let sumVolume = mr.Item.Volume * mr.Quantity

            let need =
                if inv.Contains(mr.Item) then
                    mr.Quantity - inv.Get(mr.Item)
                else
                    mr.Quantity

            let needStr =
                if need <= 0.0 then
                    CellBuilder() { rightPad }
                else
                    CellBuilder() { integer need }

            mainTable {
                [ CellBuilder() { literal mr.Item.Name }
                  CellBuilder() { integer mr.Quantity }
                  if useInv then needStr
                  CellBuilder() { number sumVolume } ]
            }
            |> ignore

            totalInputVolume <- totalInputVolume + sumVolume

        // 总材料信息
        mainTable {
            [ CellBuilder() { literal "材料体积" }
              CellBuilder() { rightPad }
              if useInv then
                  CellBuilder() { rightPad }
              CellBuilder() { number totalInputVolume } ]
        }
        |> ignore

        if inputAcc.Count = 0 then
            cmdArg.Abort(InputError, "计算结果为空")

        mainTable

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

    [<CommandHandlerMethod("#errc",
                           "EVE蓝图成本计算",
                           "不支持表达式，但仅限一个物品。可选参数见#evehelp。如：
#errc 帝国海军散热槽*10")>]
    member x.HandleERRCV3(cmdArg: CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.HeaderArgs)

        let accIn = ItemAccumulator<EveType>()
        let accOut = ItemAccumulator<EveType>()
        let mutable accInstallCost = 0.0

        let pm = EveProcessManager(cfg)

        match er.Eval(cfg.GetNonOptionString()) with
        | Number n -> cmdArg.Abort(InputError, "结算结果为数字: {0}", n)
        | Accumulator a when a.Count = 0 -> cmdArg.Abort(InputError, "没有可供计算的物品")
        | Accumulator a ->
            for mr in a do
                let ime = cfg.GetImeAuto(mr.Item)

                let recipe = pm.TryGetRecipe(mr.Item, ByRun mr.Quantity, ime)

                if recipe.IsNone then
                    cmdArg.Abort(InputError, "找不到配方:{0}", mr.Item.Name)

                let proc = recipe.Value.ApplyFlags(MeApplied)

                accInstallCost <- accInstallCost + recipe.Value.GetInstallationCost(cfg)

                for mr in proc.Input do
                    accIn.Update(mr)

                for mr in proc.Output do
                    accOut.Update(mr)

        let priceTable =
            let table = MarketUtils.EveMarketPriceTable()

            for mr in accOut do
                table.AddObject(mr)

            table

        let tt =
            TextTable(cfg.ResponseType) {
                ToolWarning

                sprintf
                    "输入效率：%i%% 默认效率：%i%% 成本指数：%i%% 设施税率%i%%"
                    cfg.InputMe
                    cfg.DerivationMe
                    cfg.SystemCostIndex
                    cfg.StructureTax

                $"展开行星材料：%b{cfg.ExpandPlanet} 展开反应公式：%b{cfg.ExpandReaction}"

                CellBuilder() {
                    literal "产品："
                    setBold
                }

                priceTable.Table

                CellBuilder() {
                    literal "材料："
                    setBold
                }

                [ CellBuilder() { literal "材料" }
                  CellBuilder() { rightLiteral "数量" }
                  CellBuilder() { rightLiteral cfg.MaterialPriceMode }
                  CellBuilder() { rightLiteral "生产" } ]

                [ CellBuilder() { literal "制造费用" }
                  CellBuilder() { rightPad }
                  CellBuilder() { number accInstallCost }
                  CellBuilder() { rightPad } ]
            }

        let mutable optCost = accInstallCost
        let mutable allCost = accInstallCost

        for mr in accIn |> Seq.sortBy (fun x -> x.Item.MarketGroupId) do
            let price = // 市场价格
                mr.Item.GetPrice(cfg.MaterialPriceMode) * mr.Quantity

            let mrProc = pm.TryGetRecipeRecMe(mr.Item, ByItem mr.Quantity, cfg.DerivationMe, cfg.DerivationMe)

            if mrProc.IsSome && pm.CanExpand(mrProc.Value.InputProcess) then
                let mrInstall =
                    mrProc.Value.IntermediateProcess
                    |> Array.fold (fun acc proc -> acc + proc.GetInstallationCost(cfg)) 0.0

                let mrCost = mrProc.Value.FinalProcess.Input.GetPrice(cfg.MaterialPriceMode)

                let mrAll = mrInstall + mrCost
                allCost <- allCost + mrAll

                let add =
                    match Double.IsNormal(price), price, Double.IsNormal(mrAll), mrAll with
                    | false, _, false, _ -> 0.0
                    | true, add, false, _ -> add
                    | false, _, true, add -> add
                    | true, a, true, b -> min a b

                optCost <- optCost + add

                tt {
                    [ CellBuilder() { literal mr.Item.Name }
                      CellBuilder() { integer mr.Quantity }
                      CellBuilder() { number price }
                      CellBuilder() { number mrAll } ]
                }
                |> ignore
            else
                optCost <- optCost + price
                allCost <- allCost + price

                tt {
                    [ CellBuilder() { literal mr.Item.Name }
                      CellBuilder() { integer mr.Quantity }
                      CellBuilder() { number price }
                      CellBuilder() { rightPad } ]
                }
                |> ignore

        let sell = priceTable.TotalSellPrice
        let sellWithTax = priceTable.TotalSellPriceWithTax

        tt {
            [ CellBuilder() { literal "卖出/税后" }
              CellBuilder() { rightPad }
              CellBuilder() { number sell }
              CellBuilder() { number sellWithTax } ]

            [ CellBuilder() { literal "材料/最佳" }
              CellBuilder() { rightPad }
              CellBuilder() { number allCost }
              CellBuilder() { number optCost } ]

            [ CellBuilder() { literal "税后 利润" }
              CellBuilder() { rightPad }
              CellBuilder() { number (sellWithTax - allCost) }
              CellBuilder() { number (sellWithTax - optCost) } ]
        }
        |> ignore

        tt

    [<TestFixture>]
    member x.TestERRC() =
        let tc = TestContext(x)
        tc.ShouldThrow("#errc")
        tc.ShouldThrow("#errc 5*5")
        tc.ShouldThrow("#errc 军用馒头 ime:10")
        tc.ShouldThrow("#errc 军用馒头蓝图")

        tc.ShouldNotThrow("#errc 恶狼级")
        tc.ShouldNotThrow("#errc 恶狼级蓝图 ime:10")

    [<CommandHandlerMethod("#EVE舰船II", "T2舰船制造总览", "可选参数见#evehelp。")>]
    [<CommandHandlerMethod("#EVE舰船", "T1舰船制造总览", "可选参数见#evehelp。")>]
    [<CommandHandlerMethod("#EVE组件", "T2和旗舰组件制造总览", "可选参数见#evehelp。")>]
    [<CommandHandlerMethod("#EVE种菜", "EVE种菜利润", "可选参数见#evehelp。")>]
    [<CommandHandlerMethod("#EVE装备II", "EVET2装备利润", "可以使用by:搜索物品组名称。其他可选参数见#evehelp。")>]
    [<CommandHandlerMethod("#EVE燃料块", "EVE燃料块", "可选参数见#evehelp。")>]
    member x.HandleManufacturingOverview(cmdArg: CommandEventArgs) =
        let cfg = EveConfigParser()

        let searchByGroupName =
            cfg.RegisterOption(
                { new OptionCell<bool>(cfg, "by", false) with
                    override x.ConvertValue(opt) = opt = "group" }
            )

        cfg.Parse(cmdArg.HeaderArgs)

        use ret = cmdArg.OpenResponse(ForceImage)
        ret.WriteLine(ToolWarning)

        ret.WriteLine(
            sprintf
                "输入效率：%i%% 默认效率：%i%% 成本指数：%i%% 设施税率%i%%"
                cfg.InputMe
                cfg.DerivationMe
                cfg.SystemCostIndex
                cfg.StructureTax
        )

        ret.WriteLine $"展开行星材料：%b{cfg.ExpandPlanet} 展开反应公式：%b{cfg.ExpandReaction}"

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

        ret.Table {
            [ CellBuilder() { literal "方案" }
              CellBuilder() { rightLiteral "出售价格/税前卖出" }
              CellBuilder() { rightLiteral ("生产成本/" + pmStr) }
              CellBuilder() { rightLiteral "含税利润" }
              CellBuilder() { rightLiteral "交易量" }
              CellBuilder() { rightLiteral "日均利润" } ]
        }
        |> ignore

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
                                cfg.DerivationMe
                            )
                            .Value

                    // 所有基础材料的报价
                    let materialCost = proc.FinalProcess.Input.GetPrice(cfg.MaterialPriceMode)

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

                    let sellWithTax = proc.FinalProcess.Output.GetPrice(PriceFetchMode.SellWithTax)

                    let volume = data.GetItemTradeVolume(product.Item)

                    let sortIdx =
                        //(sellWithTax - cost) / cost * 100.0 |> int
                        (sellWithTax - cost) * volume / proc.FinalProcess.Output.[0].Quantity

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
                    ret.Table {
                        "" // 表格开始之前空行

                        CellBuilder() {
                            literal $">>{group.Name}<<"
                            setBold
                        }

                        [ for x in data do
                              [ CellBuilder() { literal x.Name }
                                CellBuilder() { number x.Sell }
                                CellBuilder() { number x.Cost }
                                CellBuilder() { number x.Profit }
                                CellBuilder() { integer x.Volume }
                                CellBuilder() { number x.SortIndex } ] ]
                    }
                    |> ignore)

    [<TestFixture>]
    member x.TestManufacturingOverview() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#EVE燃料块")
        tc.ShouldNotThrow("#EVE燃料块 ime:10")
        tc.ShouldNotThrow("#EVE燃料块 ime:10 sci:10")
        tc.ShouldNotThrow("#EVE装备Ii by:group 气云")
        tc.ShouldNotThrow("#EVE装备Ii 气云")
