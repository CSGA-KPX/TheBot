namespace KPX.EvePlugin.Modules.Recipe.Errc

open System

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Testing
open KPX.FsCqHttp.Utils.TextResponse

open KPX.TheBot.Host.DataModel.Recipe
open KPX.TheBot.Host.Utils.RecipeRPN

open KPX.EvePlugin.Data.EveType
open KPX.EvePlugin.Data.Process

open KPX.EvePlugin.Utils
open KPX.EvePlugin.Utils.Helpers
open KPX.EvePlugin.Utils.Config
open KPX.EvePlugin.Utils.Extensions


type private ErrcTableUtils(cfg: EveConfigParser) =
    let mutable installCost = 0.0
    let mutable optimalCost = 0.0
    let mutable manufacturingCost = 0.0

    let pm1 = EveProcessManager(cfg)
    let productTbl = MarketUtils.EveMarketPriceTable()

    member x.ProductTable = productTbl

    member val Pm2 = EveProcessManager(cfg.GetShiftedConfig())

    /// 产物的设施费用
    member x.ProductInstallCost = installCost

    member x.OptimalCost = optimalCost

    member x.ManufacturingCost = manufacturingCost

    member x.AddProduct(mr: RecipeMaterial<EveType>, accOut: ItemAccumulator<EveType>) =
        let proc = pm1.GetRecipe(mr)
        let instCost = proc.GetInstallationCost(cfg)

        installCost <- installCost + instCost
        optimalCost <- optimalCost + instCost
        manufacturingCost <- manufacturingCost + instCost

        let applied = proc.ApplyFlags(MeApplied ProcessRunRounding.RoundUp)
        productTbl.AddObject(applied.Product)

        for m in applied.Materials do
            accOut.Update(m)

    member x.AddMaterialPrice(sellPrice: float, manuPrice: float) =
        let optPrice =
            match Double.IsNormal(sellPrice), sellPrice, Double.IsNormal(manuPrice), manuPrice with
            | false, _, false, _ -> 0.0
            | true, add, false, _ -> add
            | false, _, true, add -> add
            | true, a, true, b -> min a b

        manufacturingCost <- manufacturingCost + manuPrice
        optimalCost <- optimalCost + optPrice


type EveRecipeModule() =
    inherit CommandHandlerBase()

    let er = EveExpression.EveExpression()

    [<CommandHandlerMethod("#errc",
                           "EVE蓝图成本计算",
                           "不支持表达式，但仅限一个物品。可选参数见#evehelp。如：
#errc 帝国海军散热槽*10")>]
    member x.HandleERRCV4(cmdArg: CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.HeaderArgs)
        let helper = ErrcTableUtils(cfg)
        let respType = cfg.ResponseType

        let lv1, inv =
            let inv = MaterialInventory()
            let acc = ItemAccumulator<EveType>()

            for args in cmdArg.AllArgs do
                cfg.Parse(args)
                let str = cfg.GetNonOptionString()

                if not <| String.IsNullOrWhiteSpace(str) then
                    match er.Eval(str) with
                    | Number n -> cmdArg.Abort(InputError, "结算结果为数字: {0}", n)
                    | Accumulator a when a.Count = 0 -> cmdArg.Abort(InputError, "没有可供计算的项目")
                    | Accumulator a ->

                        for mr in a.NegativeQuantityItems do
                            inv.Update(mr.Item, -mr.Quantity)

                        for mr in a.PositiveQuantityItems do
                            helper.AddProduct(mr, acc)

            inv.RentTo(acc)
            acc.ToArray() |> Array.sortBy (fun mr -> mr.Item.MarketGroupId), inv

        let tt =
            TextTable(respType) {
                $"展开行星材料：%b{cfg.ExpandPlanet} 展开反应公式：%b{cfg.ExpandReaction}"

                Literal "产品：" { bold }

                helper.ProductTable.Table

                Literal "材料：" { bold }

                AsCols [ Literal "材料"
                         RLiteral "数量"
                         RLiteral(cfg.MaterialPriceMode.ToString())
                         RLiteral "生产" ]

                AsCols [ Literal "制造费用"
                         RightPad
                         HumanSig4 helper.ProductInstallCost
                         RightPad ]
            }

        // 逐个展开次级材料
        for mr in lv1 do
            let sellPrice = mr.GetPrice(cfg.MaterialPriceMode)
            let mrProc = helper.Pm2.TryGetMaterialsRec(mr, inv)

            if mrProc.IsSome then
                let mrInstall =
                    mrProc.Value.IntermediateProcess
                    |> Array.fold
                        (fun acc info ->
                            acc
                            + info
                                .OriginProcess
                                .SetQuantity(info.Quantity)
                                .GetInstallationCost(cfg))
                        0.0

                let mrCost = mrProc.Value.FinalProcess.Materials.GetPrice(cfg.MaterialPriceMode)
                let mrAll = mrInstall + mrCost
                helper.AddMaterialPrice(sellPrice, mrAll)

                tt {
                    AsCols [ Literal mr.Item.Name
                             Integer mr.Quantity
                             HumanSig4 sellPrice
                             HumanSig4 mrAll ]
                }
                |> ignore
            else
                helper.AddMaterialPrice(sellPrice, sellPrice)

                tt {
                    AsCols [ Literal mr.Item.Name
                             Integer mr.Quantity
                             HumanSig4 sellPrice
                             RightPad ]
                }
                |> ignore

        let sell = helper.ProductTable.TotalSellPrice
        let sellWithTax = helper.ProductTable.TotalSellPriceWithTax

        tt {
            AsCols [ Literal "卖出/税后"
                     RightPad
                     HumanSig4 sell
                     HumanSig4 sellWithTax ]

            AsCols [ Literal "材料/最佳"
                     RightPad
                     HumanSig4 helper.ManufacturingCost
                     HumanSig4 helper.OptimalCost ]

            AsCols [ Literal "税后 利润"
                     RightPad
                     HumanSig4(sellWithTax - helper.ManufacturingCost)
                     HumanSig4(sellWithTax - helper.OptimalCost) ]
        }
        |> ignore

        tt

    [<TestFixture>]
    member x.TestERRC() =
        let tc = TestContext(x)
        tc.ShouldThrow("#errc 5*5")
        tc.ShouldThrow("#errc 军用馒头 ime:10")
        tc.ShouldThrow("#errc 军用馒头蓝图")

        tc.ShouldNotThrow("#errc")
        tc.ShouldNotThrow("#errc 恶狼级")
        tc.ShouldNotThrow("#errc 恶狼级蓝图 ime:10")
