module TheBot.Module.EveModule.Utils.MarketUtils

open KPX.FsCqHttp.Utils.TextTable

open BotData.EveData.Utils
open BotData.EveData.EveType

open TheBot.Module.EveModule.Utils.Helpers
open TheBot.Module.EveModule.Utils.Extensions


type EveMarketPriceTable() = 
    inherit TextTable( "名称", 
                       RightAlignCell "数量",
                       RightAlignCell <| PriceFetchMode.Sell.ToString() + "/税后",
                       RightAlignCell <| PriceFetchMode.Buy.ToString() + "/税后",
                       RightAlignCell "日均交易",
                       RightAlignCell "更新时间")

    member x.AddObject(t : EveType, q : float) = 
        RowBuilder()
            .Add(t.Name)
            .Add(q)
            .Add(let nt = t.GetPrice(PriceFetchMode.Sell) * q |> HumanReadableFloat
                 let st = t.GetPrice(PriceFetchMode.SellWithTax) * q |> HumanReadableFloat
                 sprintf "%s/%s"  nt st |> RightAlignCell)
            .Add(let nt = t.GetPrice(PriceFetchMode.Buy) * q |> HumanReadableFloat
                 let wt = t.GetPrice(PriceFetchMode.BuyWithTax) * q |> HumanReadableFloat
                 sprintf "%s/%s" nt wt |> RightAlignCell |> box)
            .Add(t.GetTradeVolume() |> HumanReadableFloat |> RightAlignCell)
            .Add(t.GetPriceInfo().Updated)
        |> x.AddRow

    member x.AddObject(m : EveMaterial) = 
        x.AddObject(m.MaterialItem, m.Quantity)