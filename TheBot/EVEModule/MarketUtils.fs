module TheBot.Module.EveModule.Utils.MarketUtils

open BotData.EveData.Utils
open BotData.EveData.EveType

open TheBot.Utils.TextTable
open TheBot.Module.EveModule.Utils.Helpers
open TheBot.Module.EveModule.Utils.Config
open TheBot.Module.EveModule.Utils.Extensions


let GetPriceTable() = 
    AutoTextTable<EveType * float>(
        [|  "名称", fun (t, q) -> t.Name |> box
            "数量", fun (t, q) -> q |> HumanReadableFloat |> box
            PriceFetchMode.Sell.ToString() + "/税后", fun (t, q) -> 
                let nt = t.GetPrice(PriceFetchMode.Sell) * q |> HumanReadableFloat
                let st   = t.GetPrice(PriceFetchMode.SellWithTax) * q |> HumanReadableFloat
                sprintf "%s/%s"  nt st |> box
            PriceFetchMode.Buy.ToString() + "/税后", fun (t, q) -> 
                let nt = t.GetPrice(PriceFetchMode.Buy) * q |> HumanReadableFloat
                let wt = t.GetPrice(PriceFetchMode.BuyWithTax) * q |> HumanReadableFloat
                sprintf "%s/%s" nt wt|> box
            "日均交易", fun (t, q) -> t.GetTradeVolume() |> HumanReadableFloat |> box
            "更新时间", fun (t, q) -> t.GetPriceInfo().Updated |> box  |])