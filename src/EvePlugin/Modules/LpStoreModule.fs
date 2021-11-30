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
    let pm = EveProcessManager.Default

    member x.ShowOverview(cmdArg : CommandEventArgs, cfg : LpConfigParser) =
        let minVol = cfg.MinimalVolume
        let minVal = cfg.MinimalValue

        use resp = cmdArg.OpenResponse(ForceImage)

        resp.Table {
            $"最低交易量比(vol)：%g{minVol} 最低LP价值(val)：%g{minVal} 结果上限(count)：%i{cfg.RecordCount}"
            "警告：请参考交易量，利润很高的不一定卖得掉"

            [ CellBuilder() { literal "兑换" }
              CellBuilder() {
                  literal "出售价格"
                  rightAlign
              }
              CellBuilder() {
                  literal "利润"
                  rightAlign
              }
              CellBuilder() {
                  literal "利润/LP"
                  rightAlign
              }
              CellBuilder() {
                  literal "交易量比"
                  rightAlign
              } ]
        }
        |> ignore



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
                    $"%s{itemOffer.Item.Name}*%g{itemOffer.Quantity}"

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
                resp.Table {
                    [ CellBuilder() { literal r.Name }
                      CellBuilder() { floatSig4 r.SellPrice }
                      CellBuilder() { floatSig4 r.Profit }
                      CellBuilder() { integer r.ProfitPerLp }
                      CellBuilder() { integer r.DailyOfferVolume } ]
                }
                |> ignore)


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

        use resp = cmdArg.OpenResponse(ForceImage)

        let profitTable =
            TextTable() {
                [ CellBuilder() { literal "名称" }
                  CellBuilder() {
                      literal "数量"
                      rightAlign
                  }
                  CellBuilder() {
                      literal "税后卖出"
                      rightAlign
                  }
                  CellBuilder() {
                      literal "交易量"
                      rightAlign
                  }
                  CellBuilder() {
                      literal "利润"
                      rightAlign
                  }
                  CellBuilder() {
                      literal "单LP价值"
                      rightAlign
                  } ]
            }

        resp.Table {
            profitTable

            ""

            "材料："

            [ CellBuilder() { literal "物品" }
              CellBuilder() { literal "数量" }
              CellBuilder() {
                  literal cfg.MaterialPriceMode
                  rightAlign
              } ]

            [ CellBuilder() { literal "忠诚点" }
              CellBuilder() { integer offer.Value.LpCost }
              CellBuilder() { rightPad } ]

            [ CellBuilder() { literal "星币" }
              CellBuilder() { rightPad }
              CellBuilder() { number offer.Value.IskCost } ]
        }
        |> ignore

        let mProc = offer.Value.CastProcess()

        for mr in mProc.Input do
            let price = mr.Item.GetPrice(cfg.MaterialPriceMode)
            let total = price * mr.Quantity
            materialPriceSum <- materialPriceSum + total

            resp.Table {
                [ CellBuilder() { literal mr.Item.Name }
                  CellBuilder() { integer mr.Quantity }
                  CellBuilder() { number total } ]
            }
            |> ignore

        let product = mProc.GetFirstProduct()

        if product.Item.IsBlueprint then
            let proc =
                { pm.GetRecipe(product.Item) with
                      TargetQuantity = ByRun product.Quantity }

            let recipe = proc.ApplyFlags(MeApplied)

            for mr in recipe.Input do
                let price = mr.Item.GetPrice(cfg.MaterialPriceMode)
                let total = price * mr.Quantity
                materialPriceSum <- materialPriceSum + total

                resp.Table {
                    [ CellBuilder() { literal mr.Item.Name }
                      CellBuilder() { integer mr.Quantity }
                      CellBuilder() { number total } ]
                }
                |> ignore

            materialPriceSum <- materialPriceSum + proc.GetInstallationCost(cfg)

            let sellPrice =
                recipe.Output.GetPrice(PriceFetchMode.SellWithTax)

            let profit = sellPrice - materialPriceSum
            let bpProduct = recipe.GetFirstProduct()

            profitTable {
                [ CellBuilder() { literal bpProduct.Item.Name }
                  CellBuilder() { integer bpProduct.Quantity }
                  CellBuilder() { number sellPrice }
                  CellBuilder() { number (bpProduct.Item.GetTradeVolume()) }
                  CellBuilder() { number profit }
                  CellBuilder() { number (profit / offer.Value.LpCost) } ]
            }
            |> ignore
        else
            let sellPrice =
                mProc.Output.GetPrice(PriceFetchMode.SellWithTax)

            let profit = sellPrice - materialPriceSum

            profitTable {
                [ CellBuilder() { literal product.Item.Name }
                  CellBuilder() { integer product.Quantity }
                  CellBuilder() { number sellPrice }
                  CellBuilder() { number (product.Item.GetTradeVolume()) }
                  CellBuilder() { number profit }
                  CellBuilder() { number (profit / offer.Value.LpCost) } ]
            }
            |> ignore

        resp.Table {
            [ CellBuilder() { literal "合计" }
              CellBuilder() { rightPad }
              CellBuilder() { number materialPriceSum } ]
        }
        |> ignore // 因为OpenResponse()

    [<CommandHandlerMethod("#eveLp",
                           "EVE LP兑换计算。",
                           "#evelp 军团名 [道具名] [vol:2] [val:2000] [count:50] [buy:]
[]内为可选参数。如果指定道具名则查询目标军团指定兑换的详细信息。
参数说明：vol 最低交易量比，val 最低LP价值，count 结果数量上限，buy 更改为买单价格")>]
    member x.HandleEveLp(cmdArg : CommandEventArgs) =
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
