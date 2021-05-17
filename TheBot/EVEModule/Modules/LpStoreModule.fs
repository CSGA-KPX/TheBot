namespace KPX.TheBot.Module.EveModule

open System

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Testing
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable
open KPX.FsCqHttp.Utils.UserOption

open KPX.TheBot.Data.CommonModule.Recipe
open KPX.TheBot.Data.EveData.Utils
open KPX.TheBot.Data.EveData.EveType
open KPX.TheBot.Data.EveData.Process

open KPX.TheBot.Module.EveModule.Utils.Config
open KPX.TheBot.Module.EveModule.Utils.Data
open KPX.TheBot.Module.EveModule.Utils.Extensions


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
    let pm = EveProcessManager.Default

    member x.ShowOverview(cmdArg : CommandEventArgs, cfg : LpConfigParser) =
        let tt =
            TextTable(
                "兑换",
                RightAlignCell "出售价格",
                RightAlignCell "利润",
                RightAlignCell "利润/LP",
                RightAlignCell "交易量比"
            )

        let minVol = cfg.MinimalVolume
        let minVal = cfg.MinimalValue

        tt.AddPreTable(
            sprintf "最低交易量比(vol)：%g 最低LP价值(val)：%g 结果上限(count)：%i" minVol minVal cfg.RecordCount
        )

        tt.AddPreTable("警告：请参考交易量，利润很高的不一定卖得掉")

        let corp =
            let cmd = cfg.GetNonOptionString()

            if String.IsNullOrWhiteSpace(cmd) then
                cmdArg.Abort(InputError, "请输入目标军团名称")

            data.GetNpcCorporation(cmd)

        data.GetLpStoreOffersByCorp(corp)
        |> Seq.map
            (fun lpOffer ->
                let oProc = lpOffer.CastProcess()
                let itemOffer = oProc.GetFirstProduct()

                let offerStr =
                    sprintf "%s*%g" itemOffer.Item.Name itemOffer.Quantity

                let mutable totalCost = 0.0 // 所有ISK开销（如果是蓝图，还有材料开销）
                let mutable dailyVolume = 0.0 // 日均交易量
                let mutable sellPrice = 0.0 // 产物卖出价格

                // LP交换
                totalCost <-
                    totalCost
                    + oProc.Input.GetPrice(cfg.MaterialPriceMode)
                    + lpOffer.IskCost

                if itemOffer.Item.IsBlueprint then
                    let recipe = pm.GetRecipe(itemOffer)
                    let rProc = recipe.ApplyFlags(MeApplied)

                    totalCost <-
                        totalCost
                        + rProc.Input.GetPrice(cfg.MaterialPriceMode)
                        + recipe.GetInstallationCost(cfg)

                    sellPrice <- rProc.Output.GetPrice(PriceFetchMode.SellWithTax)
                    dailyVolume <- data.GetItemTradeVolume(rProc.GetFirstProduct().Item)
                else
                    sellPrice <- oProc.Output.GetPrice(PriceFetchMode.SellWithTax)
                    dailyVolume <- data.GetItemTradeVolume(oProc.GetFirstProduct().Item)

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
        |> Seq.filter
            (fun r ->
                (r.ProfitPerLp >= minVal)
                && (r.DailyOfferVolume >= minVol))
        |> Seq.sortByDescending (fun r -> r.ProfitPerLp)
        |> Seq.truncate cfg.RecordCount
        |> Seq.iter
            (fun r ->
                tt.RowBuilder {
                    yield r.Name
                    yield HumanReadableSig4Float r.SellPrice
                    yield HumanReadableSig4Float r.Profit
                    yield HumanReadableInteger r.ProfitPerLp
                    yield HumanReadableInteger r.DailyOfferVolume
                }
                |> tt.AddRow)

        using (cmdArg.OpenResponse(cfg.ResponseType)) (fun x -> x.Write(tt))

    member x.ShowSingleItem(cmdArg : CommandEventArgs, cfg : LpConfigParser) =
        let corp =
            let cmd = cfg.NonOptionStrings.[0]

            if String.IsNullOrWhiteSpace(cmd) then
                cmdArg.Abort(InputError, "目标军团名称不正确")

            data.GetNpcCorporation(cmd)

        let item =
            let name =
                String.Join(' ', cfg.NonOptionStrings |> Seq.tail)

            let ret = data.TryGetItem(name)

            if ret.IsNone then
                cmdArg.Abort(InputError, "{0} 不是有效道具名", name)

            ret.Value

        let offer =
            data.GetLpStoreOffersByCorp(corp)
            |> Array.tryFind (fun offer -> offer.Process.GetFirstProduct().Item = item.Id)

        if offer.IsNone then
            cmdArg.Abort(InputError, "不能在{0}的中找到兑换{1}的交易", corp.CorporationName, item.Name)

        let mutable materialPriceSum = offer.Value.IskCost

        let tt =
            TextTable("物品", RightAlignCell "数量", RightAlignCell <| cfg.MaterialPriceMode.ToString())

        tt.AddRow("忠诚点", HumanReadableInteger offer.Value.LpCost, PaddingRight)
        tt.AddRow("星币", PaddingRight, HumanReadableSig4Float offer.Value.IskCost)

        let mProc = offer.Value.CastProcess()

        for mr in mProc.Input do
            let price = mr.Item.GetPrice(cfg.MaterialPriceMode)
            let total = price * mr.Quantity
            materialPriceSum <- materialPriceSum + total
            tt.AddRow(mr.Item.Name, mr.Quantity, HumanReadableSig4Float total)

        let product = mProc.GetFirstProduct()

        let profitTable =
            TextTable(
                "名称",
                RightAlignCell "数量",
                RightAlignCell "税后卖出",
                RightAlignCell "交易量",
                RightAlignCell "利润",
                RightAlignCell "单LP价值"
            )


        if product.Item.IsBlueprint then
            let proc =
                { pm.GetRecipe(product.Item) with
                      TargetQuantity = ByRun product.Quantity }

            let recipe = proc.ApplyFlags(MeApplied)

            for mr in recipe.Input do
                let price = mr.Item.GetPrice(cfg.MaterialPriceMode)
                let total = price * mr.Quantity
                materialPriceSum <- materialPriceSum + total
                tt.AddRow(mr.Item.Name, mr.Quantity, HumanReadableSig4Float total)

            materialPriceSum <- materialPriceSum + proc.GetInstallationCost(cfg)

            let sellPrice =
                recipe.Output.GetPrice(PriceFetchMode.SellWithTax)

            let profit = sellPrice - materialPriceSum
            let bpProduct = recipe.GetFirstProduct()

            profitTable.AddRow(
                bpProduct.Item.Name,
                HumanReadableInteger bpProduct.Quantity,
                HumanReadableSig4Float sellPrice,
                HumanReadableSig4Float(bpProduct.Item.GetTradeVolume()),
                HumanReadableSig4Float profit,
                HumanReadableSig4Float(profit / offer.Value.LpCost)
            )
        else
            let sellPrice =
                mProc.Output.GetPrice(PriceFetchMode.SellWithTax)

            let profit = sellPrice - materialPriceSum

            profitTable.AddRow(
                product.Item.Name,
                HumanReadableInteger product.Quantity,
                HumanReadableSig4Float sellPrice,
                HumanReadableSig4Float(product.Item.GetTradeVolume()),
                HumanReadableSig4Float profit,
                HumanReadableSig4Float(profit / offer.Value.LpCost)
            )

        tt.AddPreTable(profitTable)
        tt.AddPreTable("材料：")
        tt.AddRow("合计", PaddingRight, HumanReadableSig4Float materialPriceSum)

        using (cmdArg.OpenResponse(cfg.ResponseType)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("#eveLp",
                                    "EVE LP兑换计算。",
                                    "#evelp 军团名 [道具名] [vol:2] [val:2000] [count:50] [buy:]
[]内为可选参数。如果指定道具名则查询目标军团指定兑换的详细信息。
参数说明：vol 最低交易量比，val 最低LP价值，count 结果数量上限，buy 更改为买单价格")>]
    member x.HandleEveLp(cmdArg : CommandEventArgs) =
        let cfg = LpConfigParser()
        cfg.Parse(cmdArg.Arguments)

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