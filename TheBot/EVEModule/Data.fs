namespace TheBot.Module.EveModule.Utils.Data

open BotData.EveData
open BotData.EveData.EveType
open BotData.EveData.NpcCorporation


type DataBundle private () = 

    let itemCol = EveTypeCollection.Instance
    let npcCorpNames = NpcCorporationoCollection.Instance
    let priceCache = MarketPriceCache.PriceCacheCollection.Instance
    let volumeCache = MarketHistoryCache.MarketTradeHistoryCollection.Instance
    let lpStoreCache = LoyaltyStoreOffer.LoyaltyStoreCollection.Instance

    static let instance = 
        let i = DataBundle()
        i

    member x.GetGroup(groupId : int) = 
        Group.EveGroupCollection.Instance.GetByGroupId(groupId)

    member x.TryGetItem(str : string) = 
        itemCol.TryGetByName(str)

    member x.GetItem(str : string) = 
        x.TryGetItem(str).Value

    member x.GetItem(id : int) = itemCol.GetById(id)

    member x.GetItemPrice(t : EveType)  = x.GetItemPrice(t.Id)
    member x.GetItemPrice(id : int) = priceCache.Force(id)
    member x.GetItemPriceCached(t : EveType) = x.GetItemPriceCached(t.Id)
    member x.GetItemPriceCached(id : int) = priceCache.[id]

    member x.GetNpcCorporation(name : string) = npcCorpNames.GetByName(name)

    member x.GetItemTradeVolume(t : EveType) = 
        let data = volumeCache.[t.Id].History
        if data.Length <> 0 then
            data |> Array.averageBy (fun x -> x.Volume |> float)
        else 
            0.0

    member x.GetLpStoreOffersByCorp(c : NpcCorporation) = lpStoreCache.[c.Id].Offers

    static member val Instance = instance
