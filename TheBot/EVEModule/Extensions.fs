﻿module TheBot.Module.EveModule.Utils.Extensions

open EveData

open TheBot.Module.EveModule.Utils.Config
open TheBot.Module.EveModule.Utils.Data



type EveType with
    member x.GetPrice(pm : PriceFetchMode) =
        match pm with
        | PriceFetchMode.Buy -> x.GetBuyPrice()
        | PriceFetchMode.Sell -> x.GetSellPrice()

    member x.GetPriceInfo() = DataBundle.Instance.GetItemPriceCached(x)

    member x.GetSellPrice() = x.GetPriceInfo().Sell

    member x.GetBuyPrice()  = x.GetPriceInfo().Buy

    member x.GetTradeVolume() = DataBundle.Instance.GetItemTradeVolume(x)

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

    member x.GetManufacturingFee(cfg : EveConfigParser) = 
        cfg.CalculateManufacturingFee(x.GetTotalMaterialPrice(PriceFetchMode.Sell), x.Type)

    /// 获取制造费用（材料+税） LP计算用
    ///
    /// 当前蓝图不作调整，次级蓝图按照DME选项计算
    member x.GetManufacturingPrice(cfg : EveConfigParser) = 
        let cost = x.GetTotalMaterialPrice(cfg.MaterialPriceMode)
        let fee = x.GetManufacturingFee(cfg)
        let mutable sum = cost + fee

        for m in x.Materials do 
            let ret = DataBundle.Instance.TryGetBpByProduct(m.MaterialItem)
            if ret.IsSome then
                let bp = ret.Value
                sum <- sum + bp.GetBpByItemNoCeil(m.Quantity)
                               .ApplyMaterialEfficiency(cfg.DerivativetMe)
                               .GetManufacturingPrice(cfg)
        sum