module KPX.EvePlugin.Utils.MarketUtils

open KPX.FsCqHttp.Utils.TextResponse

open KPX.TheBot.Host.DataModel.Recipe

open KPX.EvePlugin.Data.Utils
open KPX.EvePlugin.Data.EveType

open KPX.EvePlugin.Utils.Helpers
open KPX.EvePlugin.Utils.Extensions


type EveMarketPriceTable() =
    let tt = TextTable(ForceImage)

    let mutable tSell = 0.0
    let mutable tBuy = 0.0
    let mutable tAdj = 0.0

    do
        tt {
            AsCols [ Literal "名称"
                     Literal "数量" { rightAlign }
                     Literal(PriceFetchMode.Sell.ToString()) { rightAlign }
                     Literal "变动" { rightAlign }
                     Literal(PriceFetchMode.Buy.ToString()) { rightAlign }
                     Literal "变动" { rightAlign }
                     Literal "日均交易" { rightAlign }
                     Literal "更新时间" { rightAlign } ]
        }
        |> ignore

    member x.Table = tt

    member x.TotalAdjustPrice = tAdj

    member x.TotalSellPrice = tSell

    member x.TotalBuyPrice = tBuy

    member x.TotalSellPriceWithTax = tSell * (float (100 - EveSellTax)) / 100.0

    member x.TotalBuyPriceWithTax = tBuy * (float (100 + EveBuyTax)) / 100.0

    member x.AddObject(t: EveType, q: float) =
        tt {
            AsCols [ Literal t.Name
                     Number q
                     let adjPrice = t.GetPrice(PriceFetchMode.AveragePrice) * q

                     tAdj <- tAdj + adjPrice
                     let sell = t.GetPrice(PriceFetchMode.Sell) * q
                     tSell <- tSell + sell
                     let ntPct = (sell - adjPrice) / adjPrice
                     HumanSig4 sell
                     Percent ntPct
                     let buy = t.GetPrice(PriceFetchMode.Buy) * q
                     tBuy <- tBuy + buy
                     let ntPct = (buy - adjPrice) / adjPrice
                     HumanSig4 buy
                     Percent ntPct
                     HumanSig4(t.GetTradeVolume())
                     TimeSpan(t.GetPriceInfo().Updated) ]
        }
        |> ignore

    member x.AddObject(m: RecipeMaterial<EveType>) = x.AddObject(m.Item, m.Quantity)
