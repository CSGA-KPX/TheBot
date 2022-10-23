namespace KPX.EvePlugin.Utils.Data

open KPX.EvePlugin.Data
open KPX.EvePlugin.Data.EveType
open KPX.EvePlugin.Data.NpcCorporation


type DataBundle private () =

    let itemCol = EveTypeCollection.Instance
    let npcCorpNames = NpcCorporationCollection.Instance

    let priceCache = MarketPriceCache.PriceCacheCollection.Instance

    let volumeCache = MarketHistoryCache.MarketTradeHistoryCollection.Instance

    let lpStoreCache = LoyaltyStoreOffer.LoyaltyStoreCollection.Instance

    member x.GetGroup(groupId: int) =
        Group.EveGroupCollection.Instance.GetByGroupId(groupId)

    member x.TryGetItem(str: string) = itemCol.TryGetByName(str)

    member x.GetItem(str: string) = x.TryGetItem(str).Value

    member x.GetItem(id: int) = itemCol.GetById(id)

    member x.GetItemPriceCached(t: EveType) = priceCache.GetItem(t.Id)
    member x.GetItemPriceCached(id: int) = priceCache.GetItem(id)

    member x.GetNpcCorporation(name: string) = npcCorpNames.GetByName(name)

    member x.GetItemTradeVolume(t: EveType) =
        volumeCache
            .GetItem(t.Id)
            .CalculateRealTradeVolume()

    member x.GetLpStoreOffersByCorp(c: NpcCorporation) = lpStoreCache.GetItem(c.Id).Offers

    static member val Instance = DataBundle()
