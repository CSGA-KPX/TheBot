module KPX.TheBot.Module.EveModule.Utils.MarketUtils

open KPX.FsCqHttp.Utils.TextTable

open KPX.TheBot.Data.CommonModule.Recipe
open KPX.TheBot.Data.EveData.Utils
open KPX.TheBot.Data.EveData.EveType

open KPX.TheBot.Module.EveModule.Utils.Helpers
open KPX.TheBot.Module.EveModule.Utils.Extensions


type EveMarketPriceTable() =
    inherit TextTable("名称",
                      RightAlignCell "数量",
                      RightAlignCell <| PriceFetchMode.Sell.ToString(),
                      RightAlignCell "变动",
                      RightAlignCell <| PriceFetchMode.Buy.ToString(),
                      RightAlignCell "变动",
                      RightAlignCell "日均交易",
                      RightAlignCell "更新时间")

    let mutable tSell = 0.0
    let mutable tBuy = 0.0
    let mutable tAdj = 0.0

    member x.TotalAdjustPrice = tAdj

    member x.TotalSellPrice = tSell

    member x.TotalBuyPrice = tBuy

    member x.TotalSellPriceWithTax = tSell * (float (100 - EveSellTax)) / 100.0

    member x.TotalBuyPriceWithTax = tBuy * (float (100 + EveBuyTax)) / 100.0


    member x.AddObject(t : EveType, q : float) =
        x.RowBuilder {
            yield t.Name
            yield q

            let adjPrice =
                t.GetPrice(PriceFetchMode.AveragePrice) * q
            tAdj <- tAdj + adjPrice
            
            let sell = t.GetPrice(PriceFetchMode.Sell) * q
            tSell <- tSell + sell
            yield HumanReadableSig4Float sell

            let ntPct =
                (sell - adjPrice)
                / adjPrice
                * 100.0
                |> HumanReadableSig4Float

            yield sprintf "%s%%" ntPct.Text |> RightAlignCell

            let buy = t.GetPrice(PriceFetchMode.Buy) * q
            tBuy <- tBuy + buy
            yield HumanReadableSig4Float buy

            let ntPct =
                (buy - adjPrice)
                / adjPrice
                * 100.0
                |> HumanReadableSig4Float

            yield sprintf "%s%%" ntPct.Text |> RightAlignCell

            yield HumanReadableSig4Float(t.GetTradeVolume())

            yield HumanTimeSpan(t.GetPriceInfo().Updated)
        }
        |> x.AddRow

    member x.AddObject(m : RecipeMaterial<EveType>) = x.AddObject(m.Item, m.Quantity)
