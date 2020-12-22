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
                      RightAlignCell
                      <| PriceFetchMode.Sell.ToString() + "/税后",
                      RightAlignCell
                      <| PriceFetchMode.Buy.ToString() + "/税后",
                      RightAlignCell "日均交易",
                      RightAlignCell "更新时间")

    member x.AddObject(t : EveType, q : float) =
        x.RowBuilder {
            yield t.Name
            yield q

            let nt =
                t.GetPrice(PriceFetchMode.Sell) * q
                |> HumanReadableFloat

            let st =
                t.GetPrice(PriceFetchMode.SellWithTax) * q
                |> HumanReadableFloat

            yield sprintf "%s/%s" nt st |> RightAlignCell

            let nt =
                t.GetPrice(PriceFetchMode.Buy) * q
                |> HumanReadableFloat

            let wt =
                t.GetPrice(PriceFetchMode.BuyWithTax) * q
                |> HumanReadableFloat

            yield sprintf "%s/%s" nt wt |> RightAlignCell

            yield
                t.GetTradeVolume()
                |> HumanReadableFloat
                |> RightAlignCell

            yield t.GetPriceInfo().Updated
        }
        |> x.AddRow

    member x.AddObject(m : RecipeMaterial<EveType>) = x.AddObject(m.Item, m.Quantity)
