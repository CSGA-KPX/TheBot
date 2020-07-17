module TheBot.Module.EveModule.Utils.Extensions

open BotData.EveData.Utils
open BotData.EveData.EveType
open BotData.EveData.EveBlueprint
open BotData.EveData.MarketPriceCache
open BotData.EveData.GameInternalPriceCache

open TheBot.Module.EveModule.Utils.Helpers
open TheBot.Module.EveModule.Utils.Config
open TheBot.Module.EveModule.Utils.Data

type EveType with
    member x.GetPrice(pm : PriceFetchMode) =
        let pi = x.GetPriceInfo()
        match pm with
        | PriceFetchMode.Buy -> pi.Buy
        | PriceFetchMode.BuyWithTax -> pi.Buy * (pct <| 100 + EveBuyTax)
        | PriceFetchMode.Sell -> pi.Sell
        | PriceFetchMode.SellWithTax -> pi.Sell * (pct <| 100 - EveSellTax)
        | PriceFetchMode.AdjustedPrice -> GameInternalPriceCollection.Instance.GetByItem(x).AdjustedPrice
        | PriceFetchMode.AveragePrice -> GameInternalPriceCollection.Instance.GetByItem(x).AveragePrice

    member x.GetPriceInfo() = DataBundle.Instance.GetItemPriceCached(x)

    member x.GetTradeVolume() = DataBundle.Instance.GetItemTradeVolume(x)

    member x.TypeGroup = DataBundle.Instance.GetGroup(x.GroupId)

    /// 不是所有物品都有市场分类
    member x.MarketGroup = BotData.EveData.EveMarketGroup.MarketGroupCollection.Instance.TryGetById(x.MarketGroupId)

type EveMaterial with
    member x.MaterialItem = DataBundle.Instance.GetItem(x.TypeId)

    /// 获取卖出价总价
    member x.GetTotalPrice(pm : PriceFetchMode) =
        x.MaterialItem.GetPrice(pm) * x.Quantity

type EveBlueprint with
    member x.ProductItem = DataBundle.Instance.GetItem(x.ProductId)

    /// 获取产品出售价（不含税）
    member x.GetTotalProductPrice(pm : PriceFetchMode) = 
        x.Products
        |> Array.sumBy (fun p -> p.GetTotalPrice(pm))

    /// 获取材料（不含税）
    member x.GetTotalMaterialPrice(pm : PriceFetchMode) = 
        x.Materials
        |> Array.sumBy (fun m -> m.GetTotalPrice(pm))

    /// 获取生产费用（近似）
    ///
    /// 目前架构不允许获取0材料蓝图，按实际产量计算
    member x.GetManufacturingFee(cfg : EveConfigParser) = 
        // 生产费用 = ME为0时的 加权物品价格
        let cost = x.GetTotalMaterialPrice(PriceFetchMode.AdjustedPrice)
        match x.Type with
        | BlueprintType.Planet -> 0.0
        | _ ->
            cost * (pct cfg.SystemCostIndex) * (100 + cfg.StructureTax |> pct)

    /// 获取制造费用（材料+税） LP计算用
    ///
    /// 当前蓝图不作调整，次级蓝图按照DME选项计算
    member x.GetManufacturingPrice(cfg : EveConfigParser) = 
        let mutable sum = x.GetManufacturingFee(cfg)

        for m in x.Materials do 
            let ret = DataBundle.Instance.TryGetBpByProduct(m.MaterialItem)
            if ret.IsSome && cfg.BpCanExpand(ret.Value) then
                let bp = ret.Value
                sum <- sum + bp.ApplyMaterialEfficiency(cfg.DerivativetMe)
                               .GetBpByItemNoCeil(m.Quantity)
                               .GetManufacturingPrice(cfg)
            else
                sum <- sum + m.GetTotalPrice(cfg.MaterialPriceMode)
        sum