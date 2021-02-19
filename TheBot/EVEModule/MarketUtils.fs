module KPX.TheBot.Module.EveModule.Utils.MarketUtils

open KPX.FsCqHttp.Utils.TextTable

open KPX.TheBot.Data.CommonModule.Recipe
open KPX.TheBot.Data.EveData.Utils
open KPX.TheBot.Data.EveData.EveType

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
                HumanReadableSig4Float(t.GetPrice(PriceFetchMode.Sell) * q)

            let st =
                HumanReadableSig4Float(t.GetPrice(PriceFetchMode.SellWithTax) * q)

            yield
                sprintf "%s/%s" nt.Text st.Text
                |> RightAlignCell

            let nt =
                HumanReadableSig4Float(t.GetPrice(PriceFetchMode.Buy) * q)

            let wt =
                HumanReadableSig4Float(t.GetPrice(PriceFetchMode.BuyWithTax) * q)

            yield
                sprintf "%s/%s" nt.Text wt.Text
                |> RightAlignCell

            yield HumanReadableInteger(t.GetTradeVolume())

            yield HumanTimeSpan(t.GetPriceInfo().Updated)
        }
        |> x.AddRow

    member x.AddObject(m : RecipeMaterial<EveType>) = x.AddObject(m.Item, m.Quantity)
