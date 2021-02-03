module KPX.TheBot.Module.EveModule.Utils.Extensions

open System.Runtime.CompilerServices

open KPX.TheBot.Data.CommonModule.Recipe

open KPX.TheBot.Data.EveData.Utils
open KPX.TheBot.Data.EveData.EveType
open KPX.TheBot.Data.EveData.Process
open KPX.TheBot.Data.EveData.MarketPriceCache
open KPX.TheBot.Data.EveData.GameInternalPriceCache

open KPX.TheBot.Module.EveModule.Utils.Helpers
open KPX.TheBot.Module.EveModule.Utils.Data
open KPX.TheBot.Module.EveModule.Utils.Config


type EveType with
    member x.GetPrice(pm : PriceFetchMode) =
        let pi = x.GetPriceInfo()

        match pm with
        | PriceFetchMode.Buy -> pi.Buy
        | PriceFetchMode.BuyWithTax -> pi.Buy * (pct <| 100 + EveBuyTax)
        | PriceFetchMode.Sell -> pi.Sell
        | PriceFetchMode.SellWithTax -> pi.Sell * (pct <| 100 - EveSellTax)
        | PriceFetchMode.AdjustedPrice ->
            GameInternalPriceCollection
                .Instance
                .GetByItem(x)
                .AdjustedPrice
        | PriceFetchMode.AveragePrice ->
            GameInternalPriceCollection
                .Instance
                .GetByItem(x)
                .AveragePrice

    member x.GetPriceInfo() =
        DataBundle.Instance.GetItemPriceCached(x)

    member x.GetTradeVolume() =
        DataBundle.Instance.GetItemTradeVolume(x)

    member x.TypeGroup = DataBundle.Instance.GetGroup(x.GroupId)

    /// 不是所有物品都有市场分类
    member x.MarketGroup =
        KPX.TheBot.Data.EveData.EveMarketGroup.MarketGroupCollection.Instance.TryGetById(x.MarketGroupId)

    member x.IsBlueprint = x.CategoryId = 9

[<Extension>]
type RecipeMaterialExtensions = 
    [<Extension>]
    static member inline GetPrice(this : RecipeMaterial<EveType> [],  pm : PriceFetchMode) =
        this
        |> Array.sumBy (fun mr -> mr.Item.GetPrice(pm) * mr.Quantity)

type EveProcess with

    /// 获取最终制造价格，配方包含数量和材料效率
    member x.GetPriceInfo(pm : PriceFetchMode) = 
        let proc = x.ApplyFlags(MeApplied)

        {| TotalProductPrice = proc.Output.GetPrice(pm)
           TotalMaterialPrice = proc.Input.GetPrice(pm) |}

    member x.GetTotalProductPrice(pm : PriceFetchMode, flag : ProcessFlag) =
        x.ApplyFlags(flag).Output.GetPrice(pm)

    /// 获取输入材料价格，可能为0
    member x.GetTotalMaterialPrice(pm : PriceFetchMode, flag : ProcessFlag) =
        x.ApplyFlags(flag).Input.GetPrice(pm)

    /// 获取生产费用。
    member x.GetInstallationCost(cfg : EveConfigParser) =
        let cost = x.ApplyFlags(QuantityApplied).Input.GetPrice(PriceFetchMode.AdjustedPrice)

        match x.Type with
        | ProcessType.Planet -> 0.0 // 行星生产税率算法麻烦，一般也不大，假定为0
        | ProcessType.Invalid ->
            raise
            <| System.NotImplementedException("非法过程")
        | ProcessType.Refine ->
            raise
            <| System.NotImplementedException("不支持计算精炼费用")
        | _ ->
            cost
            * (pct cfg.SystemCostIndex)
            * (100 + cfg.StructureTax |> pct)
