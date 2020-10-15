module TheBot.Module.EveModule.Utils.MarketUtils

open KPX.FsCqHttp.Utils.TextTable

open BotData.EveData.Utils
open BotData.EveData.EveType

open TheBot.Module.EveModule.Utils.Helpers
open TheBot.Module.EveModule.Utils.Extensions


let GetPriceTable() = 
    AutoTextTable<EveType * float>(
        [|  LeftAlignCell "名称", fun (t, _) -> t.Name |> box
            RightAlignCell "数量", fun (_, q) -> q |> HumanReadableFloat |> RightAlignCell |> box
            RightAlignCell <| PriceFetchMode.Sell.ToString() + "/税后", fun (t, q) -> 
                let nt = t.GetPrice(PriceFetchMode.Sell) * q |> HumanReadableFloat
                let st = t.GetPrice(PriceFetchMode.SellWithTax) * q |> HumanReadableFloat
                sprintf "%s/%s"  nt st |> RightAlignCell |> box
            RightAlignCell <| PriceFetchMode.Buy.ToString() + "/税后", fun (t, q) -> 
                let nt = t.GetPrice(PriceFetchMode.Buy) * q |> HumanReadableFloat
                let wt = t.GetPrice(PriceFetchMode.BuyWithTax) * q |> HumanReadableFloat
                sprintf "%s/%s" nt wt |> RightAlignCell |> box
            RightAlignCell"日均交易", fun (t, _) -> t.GetTradeVolume() |> HumanReadableFloat |> RightAlignCell |> box
            LeftAlignCell "更新时间", fun (t, _) -> t.GetPriceInfo().Updated |> box  |])