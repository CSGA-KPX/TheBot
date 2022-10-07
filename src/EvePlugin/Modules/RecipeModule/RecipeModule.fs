namespace KPX.EvePlugin.Modules.Recipe

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Testing
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.UserOption

open KPX.TheBot.Host.Utils.HandlerUtils
open KPX.TheBot.Host.DataModel.Recipe

open KPX.EvePlugin.Data.Utils
open KPX.EvePlugin.Data.EveType
open KPX.EvePlugin.Data.Process

open KPX.EvePlugin.Utils.Helpers
open KPX.EvePlugin.Utils.Config
open KPX.EvePlugin.Utils.Data
open KPX.EvePlugin.Utils.Extensions


type EveRecipeModule() =
    inherit CommandHandlerBase()

    let data = DataBundle.Instance

    [<CommandHandlerMethod("#eme", "EVE蓝图材料效率计算", "")>]
    member x.HandleME(cmdArg: CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.HeaderArgs)
        let pm2 = EveProcessManager(cfg)

        let item = data.TryGetItem(cfg.GetNonOptionString())

        if item.IsNone then
            cmdArg.Abort(InputError, "找不到物品：{0}", cfg.GetNonOptionString())

        let recipe = pm2.GetRecipe(item.Value).Set(ByRuns 1.0, 0)

        let me0Price = recipe.GetTotalMaterialPrice(PriceFetchMode.Sell, MeApplied ProcessRunRounding.AsIs)

        TextTable(cfg.ResponseType) {
            AsCols [ Literal "材料总价"
                     Integer me0Price ]

            AsCols [ Literal "材料等级"; RLiteral "节省" ]

            [ for me = 0 to 10 do
                  let cost =
                      recipe
                          .Set(ByRuns 1.0, me)
                          .GetTotalMaterialPrice(PriceFetchMode.Sell, MeApplied ProcessRunRounding.AsIs)

                  [ Literal me; Integer(me0Price - cost) ] ]
        }


    [<TestFixture>]
    member x.TestME() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#eme 恶狼级蓝图")
        tc.ShouldNotThrow("#eme 恶狼级")
        tc.ShouldThrow("#eme")
        tc.ShouldThrow("#eme 军用馒头蓝图")
        tc.ShouldThrow("#eme 军用馒头")

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
        let epm = EveProcessManager(cfg)

        ret.Table {
            AsCols [ Literal "方案"
                     RLiteral "出售价格/税前卖出"
                     RLiteral("生产成本/" + pmStr)
                     RLiteral "含税利润"
                     RLiteral "交易量"
                     RLiteral "日均利润" ]
        }
        |> ignore

        match EveProcessSearch.Instance.Search(searchCond) with
        | NoResult -> ret.Abort(InputError, "无符合要求的蓝图信息")
        | TooManyResults -> ret.Abort(InputError, "蓝图数量超限")
        | Result result ->
            result
            |> Seq.map (fun ps ->
                let product = ps.Original.Product
                let proc = epm.TryGetMaterialsRec(product).Value
                let cost = proc.GetTotalCost(cfg)
                let sellWithTax = proc.FinalProcess.Products.GetPrice(PriceFetchMode.SellWithTax)
                let volume = data.GetItemTradeVolume(product.Item)

                let sortIdx =
                    let q = (proc.FinalProcess.Products |> Seq.head).Quantity
                    //(sellWithTax - cost) / cost * 100.0 |> int
                    (sellWithTax - cost) * volume / q

                {| Name = product.Item.Name
                   TypeGroup = product.Item.TypeGroup
                   Cost = cost
                   Quantity = product.Quantity
                   Sell = proc.FinalProcess.Products.GetPrice(PriceFetchMode.Sell)
                   Profit = sellWithTax - cost
                   Volume = volume
                   SortIndex = sortIdx |})
            |> Seq.sortByDescending (fun x -> x.SortIndex)
            |> Seq.groupBy (fun x -> x.TypeGroup)
            |> Seq.iter (fun (group, data) ->
                ret.Table {
                    // 表格和表格之间空一行
                    LeftPad
                    Literal $">>{group.Name}<<" { bold }

                    [ for x in data do
                          [ Literal x.Name
                            HumanSig4 x.Sell
                            HumanSig4 x.Cost
                            HumanSig4 x.Profit
                            Integer x.Volume
                            HumanSig4 x.SortIndex ] ]
                }
                |> ignore)

    [<CommandHandlerMethod("#EVE蓝图", "蓝图制造总览（暂未开放）", "可选参数见#evehelp。", IsHidden = true)>]
    member x.HandleBlueprintOverview(cmdArg: CommandEventArgs) =
        cmdArg.EnsureSenderOwner()

        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.HeaderLine)

        use ret = cmdArg.OpenResponse(ResponseType.ForceImage)
        ret.WriteLine(ToolWarning)
        ret.WriteLine(sprintf "默认效率：%i%% 成本指数：%i%% 设施税率%i%%" cfg.DerivationMe cfg.SystemCostIndex cfg.StructureTax)
        ret.WriteLine $"展开行星材料：%b{cfg.ExpandPlanet} 展开反应公式：%b{cfg.ExpandReaction}"

        ret.Table {
            AsCols [ Literal "方案"
                     RLiteral "ime"
                     RLiteral("生产成本/" + cfg.MaterialPriceMode.ToString())
                     RLiteral "含税利润"
                     RLiteral "交易量"
                     RLiteral "日均利润" ]
        }
        |> ignore

        let data =
            let acc = ItemAccumulator<EveType * int>()

            for line in cmdArg.AllLines do
                let t = line.Split('\t')

                if t.Length > 4 then
                    let item = t.[0] |> EveTypeCollection.Instance.GetByName
                    let ime = t.[1] |> int
                    //let te = t.[2]

                    let key = item, ime
                    let runs = t.[3] |> float
                    acc.Update(key, runs)

            acc

        let result =
            [ for t in data do
                  let (item, ime) = t.Item
                  //let runs = t.Quantity
                  let cfg = EveConfigParser()
                  cfg.SetDefaultInputMe(ime)
                  cfg.Parse(System.String.Empty)

                  let epm = EveProcessManager(cfg)

                  let mr =
                      let proc = epm.GetRecipe(item)
                      proc.Original.Product

                  let proc = epm.TryGetMaterialsRec(mr).Value
                  let cost = proc.GetTotalCost(cfg)
                  let sell = proc.FinalProcess.Products.GetPrice(PriceFetchMode.SellWithTax)
                  let profit = sell - cost
                  let volume = mr.Item.GetTradeVolume()

                  yield
                      {| Name = item.Name
                         IMe = ime
                         QuantityPerRun = mr.Quantity
                         Quantity = t.Quantity * mr.Quantity
                         Volume = volume
                         Sell = sell
                         Cost = cost
                         Profit = profit |} ]
            |> List.sortByDescending (fun x -> x.Volume * x.Profit / x.QuantityPerRun)

        ret.Table {
            [ for r in result do
                  [ Literal r.Name
                    RLiteral r.IMe
                    HumanSig4 r.Cost
                    HumanSig4 r.Profit
                    HumanSig4I r.Volume
                    HumanSig4(r.Volume * r.Profit / r.QuantityPerRun) ] ]
        }
        |> ignore

    [<TestFixture>]
    member x.TestManufacturingOverview() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#EVE燃料块")
        tc.ShouldNotThrow("#EVE燃料块 ime:10")
        tc.ShouldNotThrow("#EVE燃料块 ime:10 sci:10")
        tc.ShouldNotThrow("#EVE装备Ii by:group 气云")
        tc.ShouldNotThrow("#EVE装备Ii 气云")
