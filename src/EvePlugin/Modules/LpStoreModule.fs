namespace KPX.EvePlugin.Modules

open System

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Testing
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.UserOption

open KPX.TheBot.Host.DataModel.Recipe

open KPX.EvePlugin.Data.Utils
open KPX.EvePlugin.Data.EveType
open KPX.EvePlugin.Data.Process

open KPX.EvePlugin.Utils.Config
open KPX.EvePlugin.Utils.Data
open KPX.EvePlugin.Utils.Extensions


type LpConfigParser() as x =
    inherit EveConfigParser()

    let minVolume = OptionCellSimple(x, "vol", 2.0)
    let minValue = OptionCellSimple(x, "val", 2000.0)
    let maxCount = OptionCellSimple(x, "count", 50)

    member x.MinimalVolume = minVolume.Value
    member x.MinimalValue = minValue.Value

    member x.RecordCount =
        let ret = maxCount.Value
        if ret = 0 then Int32.MaxValue else ret

type EveLpStoreModule() =
    inherit CommandHandlerBase()

    let data = DataBundle.Instance

    let pm =
        EveProcessManager(
            { new IEveCalculatorConfig with
                member x.InputMe = 0
                member x.DerivationMe = 0
                member x.ExpandPlanet = false
                member x.ExpandReaction = false
                member x.RunRounding = ProcessRunRounding.RoundUp }
        )

    member x.ShowOverview(cmdArg: CommandEventArgs, cfg: LpConfigParser) =
        let minVol = cfg.MinimalVolume
        let minVal = cfg.MinimalValue

        use resp = cmdArg.OpenResponse(ForceImage)

        resp.Table {
            $"最低交易量比(vol)：%g{minVol} 最低LP价值(val)：%g{minVal} 结果上限(count)：%i{cfg.RecordCount}"
            "警告：请参考交易量，利润很高的不一定卖得掉"

            AsCols [ Literal "兑换"
                     RLiteral "出售价格"
                     RLiteral "利润"
                     RLiteral "利润/LP"
                     RLiteral "交易量比" ]
        }
        |> ignore

        let corp =
            let cmd = cfg.GetNonOptionString()

            if String.IsNullOrWhiteSpace(cmd) then
                cmdArg.Abort(InputError, "请输入目标军团名称")

            data.GetNpcCorporation(cmd)

        data.GetLpStoreOffersByCorp(corp)
        |> Seq.map (fun lpOffer ->
            let oProc = lpOffer.CastProcess()
            let itemOffer = oProc.Product

            let offerStr = $"%s{itemOffer.Item.Name}*%g{itemOffer.Quantity}"

            let mutable totalCost = 0.0 // 所有ISK开销（如果是蓝图，还有材料开销）
            let mutable dailyVolume = 0.0 // 日均交易量
            let mutable sellPrice = 0.0 // 产物卖出价格

            // LP交换
            totalCost <- totalCost + oProc.Materials.GetPrice(cfg.MaterialPriceMode) + lpOffer.IskCost

            if itemOffer.Item.IsBlueprint then
                let recipe =
                    pm
                        .GetRecipe(itemOffer.Item)
                        .SetQuantity(ByItems itemOffer.Quantity)

                let rProc = recipe.ApplyFlags(MeApplied ProcessRunRounding.RoundUp)

                totalCost <-
                    totalCost
                    + rProc.Materials.GetPrice(cfg.MaterialPriceMode)
                    + recipe.GetInstallationCost(cfg)

                sellPrice <- rProc.Product.GetPrice(PriceFetchMode.SellWithTax)
                dailyVolume <- data.GetItemTradeVolume(rProc.Product.Item)
            else
                sellPrice <- oProc.Product.GetPrice(PriceFetchMode.SellWithTax)
                dailyVolume <- data.GetItemTradeVolume(oProc.Product.Item)

            {| Name = offerStr
               OfferItem = itemOffer.Item
               OfferQuantity = itemOffer.Quantity
               TotalCost = totalCost
               SellPrice = sellPrice
               Profit = sellPrice - totalCost
               ProfitPerLp = (sellPrice - totalCost) / lpOffer.LpCost
               Volume = dailyVolume
               DailyOfferVolume = dailyVolume / itemOffer.Quantity
               LpCost = lpOffer.LpCost |})
        |> Seq.filter (fun r -> (r.ProfitPerLp >= minVal) && (r.DailyOfferVolume >= minVol))
        |> Seq.sortByDescending (fun r -> r.ProfitPerLp)
        |> Seq.truncate cfg.RecordCount
        |> Seq.iter (fun r ->
            resp.Table {
                AsCols [ Literal r.Name
                         HumanSig4 r.SellPrice
                         HumanSig4 r.Profit
                         Integer r.ProfitPerLp
                         Integer r.DailyOfferVolume ]
            }
            |> ignore)


    member x.ShowSingleItem(cmdArg: CommandEventArgs, cfg: LpConfigParser) =
        let corp =
            let cmd = cfg.NonOptionStrings.[0]

            if String.IsNullOrWhiteSpace(cmd) then
                cmdArg.Abort(InputError, "目标军团名称不正确")

            data.GetNpcCorporation(cmd)

        let item =
            let name = String.Join(' ', cfg.NonOptionStrings |> Seq.tail)

            let ret = data.TryGetItem(name)

            if ret.IsNone then
                cmdArg.Abort(InputError, "{0} 不是有效道具名", name)

            ret.Value

        let offer =
            data.GetLpStoreOffersByCorp(corp)
            |> Array.tryFind (fun offer -> offer.Process.Product.Item = item.Id)

        if offer.IsNone then
            cmdArg.Abort(InputError, "不能在{0}的中找到兑换{1}的交易", corp.CorporationName, item.Name)

        let mutable materialPriceSum = offer.Value.IskCost

        use resp = cmdArg.OpenResponse(ForceImage)

        let profitTable =
            TextTable() {
                AsCols [ Literal "名称"
                         RLiteral "数量"
                         RLiteral "税后卖出"
                         RLiteral "交易量"
                         RLiteral "利润"
                         RLiteral "单LP价值" ]
            }

        resp.Table {
            profitTable

            ""

            "材料："

            AsCols [ Literal "物品"
                     RLiteral "数量"
                     RLiteral(cfg.MaterialPriceMode.ToString()) ]

            AsCols [ Literal "忠诚点"
                     Integer offer.Value.LpCost
                     RightPad ]

            AsCols [ Literal "星币"
                     RightPad
                     HumanSig4 offer.Value.IskCost ]
        }
        |> ignore

        let mProc = offer.Value.CastProcess()

        for mr in mProc.Materials do
            let price = mr.Item.GetPrice(cfg.MaterialPriceMode)
            let total = price * mr.Quantity
            materialPriceSum <- materialPriceSum + total

            resp.Table {
                AsCols [ Literal mr.Item.Name
                         Integer mr.Quantity
                         HumanSig4 total ]
            }
            |> ignore

        let product = mProc.Product

        if product.Item.IsBlueprint then
            let proc = { pm.GetRecipe(product.Item) with TargetQuantity = ByRuns product.Quantity }

            let recipe = proc.ApplyFlags(MeApplied ProcessRunRounding.RoundUp)

            for mr in recipe.Materials do
                let price = mr.Item.GetPrice(cfg.MaterialPriceMode)
                let total = price * mr.Quantity
                materialPriceSum <- materialPriceSum + total

                resp.Table {
                    AsCols [ Literal mr.Item.Name
                             Integer mr.Quantity
                             HumanSig4 total ]
                }
                |> ignore

            materialPriceSum <- materialPriceSum + proc.GetInstallationCost(cfg)

            let sellPrice = recipe.Product.GetPrice(PriceFetchMode.SellWithTax)

            let profit = sellPrice - materialPriceSum
            let bpProduct = recipe.Product

            profitTable {
                AsCols [ Literal bpProduct.Item.Name
                         Integer bpProduct.Quantity
                         HumanSig4 sellPrice
                         HumanSig4(bpProduct.Item.GetTradeVolume())
                         HumanSig4 profit
                         HumanSig4(profit / offer.Value.LpCost) ]
            }
            |> ignore
        else
            let sellPrice = mProc.Product.GetPrice(PriceFetchMode.SellWithTax)

            let profit = sellPrice - materialPriceSum

            profitTable {
                AsCols [ Literal product.Item.Name
                         Integer product.Quantity
                         HumanSig4 sellPrice
                         HumanSig4(product.Item.GetTradeVolume())
                         HumanSig4 profit
                         HumanSig4(profit / offer.Value.LpCost) ]
            }
            |> ignore

        resp.Table {
            AsCols [ Literal "合计"
                     RightPad
                     HumanSig4 materialPriceSum ]
        }
        |> ignore // 因为OpenResponse()

    [<CommandHandlerMethod("#eveLp",
                           "EVE LP兑换计算。",
                           "#evelp 军团名 [道具名] [vol:2] [val:2000] [count:50] [buy:]
[]内为可选参数。如果指定道具名则查询目标军团指定兑换的详细信息。
参数说明：vol 最低交易量比，val 最低LP价值，count 结果数量上限，buy 更改为买单价格")>]
    member x.HandleEveLp(cmdArg: CommandEventArgs) =
        let cfg = LpConfigParser()
        cfg.Parse(cmdArg.HeaderArgs)

        match cfg.NonOptionStrings.Count with
        | 0 -> cmdArg.Abort(InputError, "请输入目标军团名称")
        | 1 -> x.ShowOverview(cmdArg, cfg)
        | _ -> x.ShowSingleItem(cmdArg, cfg)

    [<TestFixture>]
    member x.TestLP() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#evelp 姐妹会 val:5 val:3000 count:999")
        tc.ShouldNotThrow("#evelp 姐妹会 val:5 val:3000 count:999 buy:")
        tc.ShouldNotThrow("#evelp 姐妹会 姐妹会延伸探针发射器")
