module TheBot.Module.EveModule.Extensions
open EveData
open TheBot.Module.EveModule.Utils

type EveType with
    member x.GetSellPrice() = GetItemPriceCached(x.TypeId)

type EveMaterial with
    member x.MaterialItem = EveTypeIdCache.[x.TypeId]
    /// 获取卖出价总价
    member x.GetTotalPrice() = GetItemPriceCached(x.TypeId) * x.Quantity

type EveBlueprint with
    member x.ProductItem = EveTypeIdCache.[x.ProductId]

    /// 获取产品出售价（不含税）
    member x.GetTotalProductPrice() = GetItemPriceCached(x.ProductId) * x.ProductQuantity

    /// 获取材料（不含税）
    member x.GetTotalMaterialPrice() = 
        x.Materials
        |> Array.sumBy (fun m -> m.GetTotalPrice())