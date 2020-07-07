namespace TheBot.Module.EveModule.Utils.Data

open System.Collections.Generic

open BotData.EveData
open BotData.EveData.EveType
open BotData.EveData.EveBlueprint
open BotData.EveData.NpcCorporation


type DataBundle private () = 
    let logger = NLog.LogManager.GetCurrentClassLogger()
    
    let itemCol = EveTypeCollection.Instance
    let bpCol   = EveBlueprintCollection.Instance

    let npcCorpNames = NpcCorporationoCollection.Instance
    let refineInfo = RefineInfo.RefineInfoCollection.Instance
    let priceCache = MarketPriceCache.PriceCacheCollection.Instance
    let volumeCache = MarketHistoryCache.MarketTradeHistoryCollection.Instance
    let lpStoreCache = LoyaltyStoreOffer.LoyaltyStoreCollection.Instance

    let itemNotFound = failwithf "找不到物品 %s"

    static let instance = 
        let i = DataBundle()
        i

    member x.TryGetItem(str : string) = 
        itemCol.TryGetByName(str)

    member x.GetItem(str : string) = 
        x.TryGetItem(str).Value

    member x.GetItem(id : int) = itemCol.GetById(id)

    member x.TryTypeToBp(t : EveType) = 
        let ret = bpCol.TryGetByKey(t.Id)
        if ret.IsNone then
            x.TryGetBpByProduct(t)
        else
            ret

    member x.TryGetBp(id : int) = 
        bpCol.TryGetByKey(id)

    member x.GetBps() = bpCol :> IEnumerable<EveBlueprint>

    member x.GetBp(id : int) = bpCol.GetByKey(id)

    member x.GetBpByProduct(id : int) = bpCol.GetByProduct(id)

    member x.GetBpByProduct(t : EveType) = x.GetBpByProduct(t.Id)

    member x.TryGetBpByProduct(t : EveType) = bpCol.TryGetByProduct(t)

    member x.GetItemPrice(t : EveType)  = x.GetItemPrice(t.Id)
    member x.GetItemPrice(id : int) = priceCache.Force(id)
    member x.GetItemPriceCached(t : EveType) = x.GetItemPriceCached(t.Id)
    member x.GetItemPriceCached(id : int) = priceCache.[id]

    member x.GetNpcCorporation(name : string) = npcCorpNames.GetByName(name)

    member x.GetItemTradeVolume(t : EveType) = volumeCache.[t.Id].History |> Array.averageBy (fun x -> x.Volume |> float)

    member x.GetLpStoreOffersByCorp(c : NpcCorporation) = lpStoreCache.[c.Id].Offers

    member x.GetRefineInfo(t : EveType) = refineInfo.GetByItem(t)

    static member val Instance = instance
