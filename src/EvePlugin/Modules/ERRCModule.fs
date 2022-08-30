namespace KPX.EvePlugin.Modules.Errc

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


type private EvePriceCalcHelper(cfg: EveConfigParser, ctx: RecipeCalculationContext<EveType, EveProcess>) =
    let mutable installCost = 0.0
    let mutable optimalCost = 0.0
    let mutable manufacturingCost = 0.0

    let priceTable = MarketUtils.EveMarketPriceTable()

    do
        for mr in ctx.Products do
            // 产物价格表
            priceTable.AddObject(mr)

            // 配方相关计算
            let recipe = ctx.GetRecipe(mr.Item)

            // 设施费用
            let instCost =
                recipe
                    .SetQuantity(ByItem mr.Quantity)
                    .GetInstallationCost(cfg)

            installCost <- installCost + instCost
            optimalCost <- optimalCost + installCost
            manufacturingCost <- manufacturingCost + installCost

            // 材料
            // 这里我们关注成本，所以不流程考虑进位
            let proc =
                recipe
                    .Set(ByItem mr.Quantity, cfg.GetImeAuto(mr.Item))
                    .ApplyFlags(ProcessFlag.MeApplied ProcessRunRounding.AsIs)

            for mr in proc.Input do
                ctx.Materials.Update(mr)


    /// 产物的设施费用
    member x.ProductInstallCost = installCost

    member x.OptimalCost = optimalCost

    member x.ManufacturingCost = manufacturingCost

    member x.ProductPriceTable = priceTable

    member x.LoadAllPrice() =
        ctx.GetAllItems()
        |> Array.Parallel.iter (fun item -> item.GetPriceInfo() |> ignore)

    member x.TryGetRecipeRec(mr: RecipeMaterial<EveType>, ime: int, dme: int) =
        ctx.TryGetRecipe(mr.Item)
        |> Option.map (fun r ->
            let intermediate = ResizeArray<EveProcess>()
            let acc = RecipeProcessAccumulator<EveType>()

            let rec Calc i (q: float) me =
                let recipe = ctx.TryGetRecipe(i)

                if recipe.IsNone then
                    acc.Input.Update(i, q)
                else
                    let recipe = recipe.Value.Set(ByItem q, me)
                    intermediate.Add(recipe)
                    let proc = recipe.ApplyFlags(MeApplied ProcessRunRounding.AsIs)

                    for m in proc.Input do
                        Calc m.Item m.Quantity dme

            acc.Output.Update(r.Original.GetFirstProduct().Item, mr.Quantity)
            Calc mr.Item mr.Quantity ime

            {| InputProcess = r
               InputRuns =
                (ByItem mr.Quantity)
                    .ToRuns(r.Original, ProcessRunRounding.AsIs)
               FinalProcess = acc.AsRecipeProcess()
               IntermediateProcess = intermediate.ToArray() |})

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
        let pm2 = EveProcessManager2(cfg)

        let ctx =
            match er.Eval(cfg.GetNonOptionString()) with
            | Number n -> cmdArg.Abort(InputError, "结算结果为数字: {0}", n)
            | Accumulator a when a.Count = 0 -> cmdArg.Abort(InputError, "没有可供计算的项目")
            | Accumulator a ->
                let mrs = a.AsMaterials()
                let ctx = pm2.GetRecipeContext(mrs)

                for mr in mrs do
                    if mr.Quantity < 0 then
                        ctx.AddInventory(mr.Item, -mr.Quantity)
                    else
                        ctx.Products.Update(mr)

                ctx

        let calc = EvePriceCalcHelper(cfg, ctx)
        calc.LoadAllPrice()

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

                Literal "产品：" { bold }

                calc.ProductPriceTable.Table

                Literal "材料：" { bold }

                AsCols [ Literal "材料"
                         RLiteral "数量"
                         RLiteral(cfg.MaterialPriceMode.ToString())
                         RLiteral "生产" ]

                AsCols [ Literal "制造费用"
                         RightPad
                         HumanSig4 calc.ProductInstallCost
                         RightPad ]
            }

        let materials = ctx.Materials |> Seq.sortBy (fun x -> x.Item.MarketGroupId) |> Seq.toArray

        for mr in materials do
            let sellPrice = // 市场价格
                mr.Item.GetPrice(cfg.MaterialPriceMode) * mr.Quantity

            // 已经是衍生材料了，ime=dme
            let mrProc = calc.TryGetRecipeRec(mr, cfg.DerivationMe, cfg.DerivationMe)

            if mrProc.IsSome then
                let mrInstall =
                    mrProc.Value.IntermediateProcess
                    |> Array.fold (fun acc proc -> acc + proc.GetInstallationCost(cfg)) 0.0

                let manuCost = mrProc.Value.FinalProcess.Input.GetPrice(cfg.MaterialPriceMode)
                let manuAll = mrInstall + manuCost
                calc.AddMaterialPrice(sellPrice, manuAll)

                tt {
                    AsCols [ Literal mr.Item.Name
                             Integer mr.Quantity
                             HumanSig4 sellPrice
                             HumanSig4 manuAll ]
                }
                |> ignore
            else
                calc.AddMaterialPrice(sellPrice, sellPrice)

                tt {
                    AsCols [ Literal mr.Item.Name
                             Integer mr.Quantity
                             HumanSig4 sellPrice
                             RightPad ]
                }
                |> ignore

        let sell = calc.ProductPriceTable.TotalSellPrice
        let sellWithTax = calc.ProductPriceTable.TotalSellPriceWithTax

        tt {
            AsCols [ Literal "卖出/税后"
                     RightPad
                     HumanSig4 sell
                     HumanSig4 sellWithTax ]

            AsCols [ Literal "材料/最佳"
                     RightPad
                     HumanSig4 calc.ManufacturingCost
                     HumanSig4 calc.OptimalCost ]

            AsCols [ Literal "税后 利润"
                     RightPad
                     HumanSig4(sellWithTax - calc.ManufacturingCost)
                     HumanSig4(sellWithTax - calc.OptimalCost) ]
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
