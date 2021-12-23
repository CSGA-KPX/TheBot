namespace KPX.XivPlugin.Data.Shop

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb

open KPX.XivPlugin
open KPX.XivPlugin.Data

open LiteDB


[<CLIMutable>]
type GilShopInfo =
    { [<BsonId>]
      LiteDbId: int
      Region: VersionRegion
      ItemId: int32
      AskPrice: int32
      BidPrice: int32 }

type GilShopCollection private () =
    inherit CachedTableCollection<GilShopInfo>()

    static member val Instance = GilShopCollection()

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        x.DbCollection.EnsureIndex(fun x -> x.ItemId) |> ignore

        seq {
            let col = ChinaDistroData.GetCollection()

            for row in col.GilShopItem.TypedRows do
                let item = row.Item.AsRow()

                yield
                    { LiteDbId = 0
                      ItemId = item.Key.Main
                      Region = VersionRegion.China
                      AskPrice = item.``Price{Mid}``.AsInt()
                      BidPrice = item.``Price{Low}``.AsInt() }

            let col = OfficalDistroData.GetCollection()

            for row in col.GilShopItem.TypedRows do
                let item = row.Item.AsRow()

                yield
                    { LiteDbId = 0
                      ItemId = item.Key.Main
                      Region = VersionRegion.Offical
                      AskPrice = item.``Price{Mid}``.AsInt()
                      BidPrice = item.``Price{Low}``.AsInt() }
        }
        |> Seq.distinctBy (fun x -> x.ItemId)
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.TryLookupByItem(item: XivItem, region) =
        let itemId = item.ItemId
        x.DbCollection.TryQueryOne(fun x -> x.ItemId = itemId && x.Region = region)
