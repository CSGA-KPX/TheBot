module KPX.EvePlugin.Utils.Extensions

open System.Runtime.CompilerServices

open KPX.TheBot.Host.DataModel.Recipe

open KPX.EvePlugin.Data.Utils
open KPX.EvePlugin.Data.EveType
open KPX.EvePlugin.Data.Process
open KPX.EvePlugin.Data.MarketPriceCache
open KPX.EvePlugin.Data.GameInternalPriceCache

open KPX.EvePlugin.Utils.Helpers
open KPX.EvePlugin.Utils.Data
open KPX.EvePlugin.Utils.Config


type EveType with
    member x.GetPrice(pm: PriceFetchMode) =
        let pi = x.GetPriceInfo()

        match pm with
        | PriceFetchMode.BasePrice -> x.BasePrice
        | PriceFetchMode.Buy -> pi.Buy
        | PriceFetchMode.BuyWithTax -> pi.Buy * (pct <| 100 + EveBuyTax)
        | PriceFetchMode.Sell -> pi.Sell
        | PriceFetchMode.SellWithTax -> pi.Sell * (pct <| 100 - EveSellTax)
        | PriceFetchMode.AdjustedPrice ->
            GameInternalPriceCollection
                .Instance
                .GetByItem(
                    x
                )
                .AdjustedPrice
        | PriceFetchMode.AveragePrice ->
            GameInternalPriceCollection
                .Instance
                .GetByItem(
                    x
                )
                .AveragePrice

    member x.GetPriceInfo() =
        DataBundle.Instance.GetItemPriceCached(x)

    member x.GetTradeVolume() =
        DataBundle.Instance.GetItemTradeVolume(x)

    member x.TypeGroup = DataBundle.Instance.GetGroup(x.GroupId)

    /// 不是所有物品都有市场分类
    member x.MarketGroup =
        KPX.EvePlugin.Data.EveMarketGroup.MarketGroupCollection.Instance.TryGetById(x.MarketGroupId)

    member x.IsBlueprint = x.CategoryId = 9

[<Extension>]
type RecipeMaterialExtensions =
    [<Extension>]
    static member inline GetPrice(this: seq<RecipeMaterial<EveType>>, pm: PriceFetchMode) =
        this |> Seq.sumBy (fun mr -> mr.Item.GetPrice(pm) * mr.Quantity)

    [<Extension>]
    static member inline GetPrice(this: RecipeMaterial<EveType>, pm: PriceFetchMode) =
        this.Item.GetPrice(pm) * this.Quantity

type EveProcess with

    /// 获取最终制造价格，配方包含数量和材料效率
    member x.GetPriceInfo(pm: PriceFetchMode) =
        let proc = x.ApplyFlags(MeApplied ProcessRunRounding.RoundUp)

        {| TotalProductPrice = proc.Product.GetPrice(pm)
           TotalMaterialPrice = proc.Materials.GetPrice(pm) |}

    member x.GetTotalProductPrice(pm: PriceFetchMode, flag: ProcessFlag) = x.ApplyFlags(flag).Product.GetPrice(pm)

    /// 获取输入材料价格，可能为0
    member x.GetTotalMaterialPrice(pm: PriceFetchMode, flag: ProcessFlag) =
        x.ApplyFlags(flag).Materials.GetPrice(pm)

    /// 获取生产费用
    ///
    /// 行星开发计算材料的进口税和产物的出口税
    member x.GetInstallationCost(cfg: EveConfigParser) =
        match x.Type with
        | ProcessType.Planet ->
            let getBaseCost (t: EveType) =
                match t.GroupId with
                | 1035 -> 5.0 // 行星有机物 - 原始资源
                | 1042 -> 400.0 // 基础资源物品 - 第一等级
                | 1034 -> 7_200.0 // 加工过的资源物品 - 第二等级
                | 1040 -> 60_000.0 // 特种资源物品 - 第三等级
                | 1041 -> 1_200_000.0 // 高级资源物品 - 第四等级
                | _ -> invalidArg "EveType" "输入物品不是行星产物"

            let proc = x.ApplyFlags(QuantityApplied ProcessRunRounding.AsIs)

            let importFee =
                proc.Materials
                |> Array.fold (fun acc mr -> acc + mr.Quantity * (getBaseCost mr.Item * 0.5)) 0.0

            let exportFee = (getBaseCost proc.Product.Item) * proc.Product.Quantity

            (importFee + exportFee) * 0.1 // NPC税率10%
        | ProcessType.Invalid -> raise <| System.NotImplementedException("非法过程")
        | ProcessType.Refine -> raise <| System.NotImplementedException("不支持计算精炼费用")
        | _ ->
            x
                .ApplyFlags(QuantityApplied ProcessRunRounding.AsIs)
                .Materials.GetPrice(PriceFetchMode.AdjustedPrice)
            * (pct cfg.SystemCostIndex)
            * (100 + cfg.StructureTax |> pct)

[<Extension>]
type RecRecipeExtensions =
    [<Extension>]
    static member inline GetInstallationCost(proc: MaterialsRecContext<EveType, EveProcess>, cfg: EveConfigParser) =
        let baseProc = proc.IntermediateProcess |> Array.minBy (fun i -> i.IsProduct)

        if baseProc.OriginProcess.Type = ProcessType.Planet then
            // 构造一个临时配方去计算费用
            { Original =
                { Materials = proc.FinalProcess.Materials |> Seq.toArray
                  Product = proc.FinalProcess.Products |> Seq.head }
              TargetQuantity = ByRuns 1.0
              TargetMe = 0
              Type = ProcessType.Planet }
                .GetInstallationCost(cfg)
        else
            proc.IntermediateProcess
            |> Array.fold
                (fun acc info ->
                    acc
                    + info
                        .OriginProcess
                        .SetQuantity(info.Quantity)
                        .GetInstallationCost(cfg))
                0.0

    [<Extension>]
    static member inline GetTotalCost(proc: MaterialsRecContext<EveType, EveProcess>, cfg: EveConfigParser) =
        let materialCost = proc.FinalProcess.Materials.GetPrice(cfg.MaterialPriceMode)
        let installCost = proc.GetInstallationCost(cfg)
        materialCost + installCost
