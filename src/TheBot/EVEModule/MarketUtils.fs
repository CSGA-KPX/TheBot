module KPX.TheBot.Module.EveModule.Utils.MarketUtils

open KPX.FsCqHttp.Utils.TextTable

open KPX.TheBot.Data.CommonModule.Recipe
open KPX.TheBot.Data.EveData.Utils
open KPX.TheBot.Data.EveData.EveType

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

    member x.AddObject(t : EveType, q : float) =
        x.RowBuilder {
            yield t.Name
            yield q

            let adjPrice =
                t.GetPrice(PriceFetchMode.AveragePrice) * q

            let nt =
                HumanReadableSig4Float(t.GetPrice(PriceFetchMode.Sell) * q)

            yield nt

            let ntPct =
                ((t.GetPrice(PriceFetchMode.Sell) * q) - adjPrice)
                / adjPrice
                * 100.0
                |> HumanReadableSig4Float

            yield sprintf "%s%%" ntPct.Text |> RightAlignCell

            let nt =
                HumanReadableSig4Float(t.GetPrice(PriceFetchMode.Buy) * q)

            yield nt

            let ntPct =
                ((t.GetPrice(PriceFetchMode.Buy) * q) - adjPrice)
                / adjPrice
                * 100.0
                |> HumanReadableSig4Float

            yield sprintf "%s%%" ntPct.Text |> RightAlignCell

            yield HumanReadableSig4Float(t.GetTradeVolume())

            yield HumanTimeSpan(t.GetPriceInfo().Updated)
        }
        |> x.AddRow

    member x.AddObject(m : RecipeMaterial<EveType>) = x.AddObject(m.Item, m.Quantity)
