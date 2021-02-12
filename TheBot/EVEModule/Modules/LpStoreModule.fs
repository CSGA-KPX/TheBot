﻿namespace KPX.TheBot.Module.EveModule

open System

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

    member x.ShowOverview(cmdArg : CommandEventArgs, cfg : LpConfigParser) =
        let tt =
            TextTable("兑换", RightAlignCell "利润", RightAlignCell "利润/LP", RightAlignCell "日均交易")

        let minVol = cfg.MinimalVolume
        let minVal = cfg.MinimalValue

        tt.AddPreTable(sprintf "最低交易量(vol)：%g 最低LP价值(val)：%g 结果上限(count)：%i" minVol minVal cfg.RecordCount)

        tt.AddPreTable("警告：请参考交易量，利润很高的不一定卖得掉")

        let corp =
            let cmd = cfg.CmdLineAsString

            if String.IsNullOrWhiteSpace(cmd) then cmdArg.AbortExecution(InputError, "请输入目标军团名称")

            data.GetNpcCorporation(cmd)

        data.GetLpStoreOffersByCorp(corp)
        |> Array.map
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

    member x.ShowSingleItem(cmdArg : CommandEventArgs, cfg : LpConfigParser) =
        let corp =
            let cmd = cfg.CommandLine.[0]
            if String.IsNullOrWhiteSpace(cmd) then cmdArg.AbortExecution(InputError, "目标军团名称不正确")
            data.GetNpcCorporation(cmd)

        let item =
            let name = String.Join(' ', cfg.CommandLine.[1..])
            let ret = data.TryGetItem(name)
            if ret.IsNone then cmdArg.AbortExecution(InputError, "{0} 不是有效道具名", name)
            ret.Value

        let offer =
            data.GetLpStoreOffersByCorp(corp)
            |> Array.tryFind (fun offer -> offer.Process.GetFirstProduct().Item = item.Id)

        if offer.IsNone
        then cmdArg.AbortExecution(InputError, "不能在{0}的中找到兑换{1}的交易", corp.CorporationName, item.Name)

        let mutable materialPriceSum = offer.Value.IskCost

        let tt =
            TextTable("物品", RightAlignCell "数量", RightAlignCell <| cfg.MaterialPriceMode.ToString())

        tt.AddRow("忠诚点", offer.Value.LpCost, PaddingRight)
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
                bpProduct.Quantity,
                HumanReadableSig4Float sellPrice,
                bpProduct.Item.GetTradeVolume()
                |> HumanReadableSig4Float,
                HumanReadableSig4Float profit,
                profit / offer.Value.LpCost
                |> HumanReadableSig4Float
            )
        else
            let sellPrice =
                mProc.Output.GetPrice(PriceFetchMode.SellWithTax)

            let profit = sellPrice - materialPriceSum

            profitTable.AddRow(
                product.Item.Name,
                product.Quantity,
                HumanReadableSig4Float sellPrice,
                product.Item.GetTradeVolume()
                |> HumanReadableSig4Float,
                HumanReadableSig4Float profit,
                profit / offer.Value.LpCost
                |> HumanReadableSig4Float
            )

        tt.AddPreTable(profitTable)
        tt.AddPreTable("材料：")
        tt.AddRow("合计", PaddingRight, HumanReadableSig4Float materialPriceSum)

        using (cmdArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("eveLp", "EVE LP兑换计算", "#evelp 军团名 (可选 道具名）")>]
    member x.HandleEveLp(cmdArg : CommandEventArgs) =
        let cfg = LpConfigParser()
        cfg.Parse(cmdArg.Arguments)

        match cfg.CommandLine.Length with
        | 0 -> cmdArg.AbortExecution(InputError, "请输入目标军团名称")
        | 1 -> x.ShowOverview(cmdArg, cfg)
        | _ -> x.ShowSingleItem(cmdArg, cfg)
