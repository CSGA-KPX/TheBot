namespace KPX.TheBot.Module.EveModule

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

open KPX.TheBot.Data.CommonModule.Recipe
open KPX.TheBot.Data.EveData.Utils
open KPX.TheBot.Data.EveData.EveType
open KPX.TheBot.Data.EveData.Process

open KPX.TheBot.Module.EveModule.Utils.Config
open KPX.TheBot.Module.EveModule.Utils.Data
open KPX.TheBot.Module.EveModule.Utils.Extensions


type LpConfigParser() as x =
    inherit EveConfigParser()

    do
        x.RegisterOption("vol", "10")
        x.RegisterOption("val", "2000")
        x.RegisterOption("count", "50")

    member x.MinimalVolume = x.GetValue<float>("vol")
    member x.MinimalValue = x.GetValue<float>("val")

    member x.RecordCount =
        let ret = x.GetValue<uint32>("count") |> int
        if ret = 0 then System.Int32.MaxValue else ret

type EveLpStoreModule() =
    inherit CommandHandlerBase()

    let data = DataBundle.Instance
    let pm = EveProcessManager.Default

    [<CommandHandlerMethodAttribute("eveLp", "EVE LP兑换计算", "#evelp 军团名")>]
    member x.HandleEveLp(cmdArg : CommandEventArgs) =
        let cfg = LpConfigParser()
        cfg.Parse(cmdArg.Arguments)

        let tt =
            TextTable("兑换", RightAlignCell "利润", RightAlignCell "利润/LP", RightAlignCell "日均交易")

        let minVol = cfg.MinimalVolume
        let minVal = cfg.MinimalValue
        tt.AddPreTable(sprintf "最低交易量(vol)：%g 最低LP价值(val)：%g 结果上限(count)：%i" minVol minVal cfg.RecordCount)
        tt.AddPreTable("警告：请参考交易量，利润很高的不一定卖得掉")

        let corp =
            data.GetNpcCorporation(cfg.CmdLineAsString)

        data.GetLpStoreOffersByCorp(corp)
        |> Array.map
            (fun lpOffer ->
                let proc = lpOffer.CastProcess()
                let itemOffer = proc.GetFirstProduct()

                let totalCost =
                    let inputCost =
                        proc.Input
                        |> Array.sumBy
                            (fun mr ->
                                mr.Item.GetPrice(cfg.MaterialPriceMode)
                                * mr.Quantity)

                    inputCost + lpOffer.IskCost

                let dailyVolume, sellPrice =
                    if itemOffer.Item.IsBlueprint then
                        let proc = pm.GetRecipe(itemOffer)

                        let price =
                            proc.GetTotalProductPrice(PriceFetchMode.SellWithTax)
                            - proc.GetInstallationCost(cfg)

                        data.GetItemTradeVolume(proc.Process.GetFirstProduct().Item), price
                    else
                        let price =
                            proc.Output
                            |> Array.sumBy
                                (fun mr ->
                                    mr.Item.GetPrice(PriceFetchMode.SellWithTax)
                                    * mr.Quantity)

                        data.GetItemTradeVolume(itemOffer.Item), price

                let offerStr =
                    sprintf "%s*%g" itemOffer.Item.Name itemOffer.Quantity

                {| Name = offerStr
                   TotalCost = totalCost
                   SellPrice = sellPrice
                   Profit = sellPrice - totalCost
                   ProfitPerLp = (sellPrice - totalCost) / lpOffer.LpCost
                   Volume = dailyVolume
                   LpCost = lpOffer.LpCost
                   Offer = itemOffer |})
        |> Array.filter (fun r -> (r.ProfitPerLp >= minVal) && (r.Volume >= minVol))
        |> Array.sortByDescending
            (fun r ->
                //let weightedVolume = r.Volume / r.Offer.Quantity
                //r.ProfitPerLp * weightedVolume)
                r.ProfitPerLp)
        |> Array.truncate cfg.RecordCount
        |> Array.iter (fun r -> tt.AddRow(r.Name, r.Profit |> floor, r.ProfitPerLp |> floor, r.Volume |> floor))

        using (cmdArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))