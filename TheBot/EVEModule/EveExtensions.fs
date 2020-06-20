module TheBot.Module.EveModule.Extensions
open EveData
open TheBot.Module.EveModule.Utils

type EveType with
    member x.GetPrice()     = GetItemPriceCached(x.TypeId)
    member x.GetSellPrice() = GetItemPriceCached(x.TypeId).Sell
    member x.GetBuyPrice()  = GetItemPriceCached(x.TypeId).Buy

type EveMaterial with
    member x.MaterialItem = EveTypeIdCache.[x.TypeId]
    /// 获取卖出价总价
    member x.GetTotalPrice() = x.MaterialItem.GetSellPrice() * x.Quantity

type EveBlueprint with
    member x.ProductItem = EveTypeIdCache.[x.ProductId]

    /// 获取产品出售价（不含税）
    member x.GetTotalProductPrice() = 
        x.Products
        |> Array.sumBy (fun p -> p.GetTotalPrice())

    /// 获取材料（不含税）
    member x.GetTotalMaterialPrice() = 
        x.Materials
        |> Array.sumBy (fun m -> m.GetTotalPrice())