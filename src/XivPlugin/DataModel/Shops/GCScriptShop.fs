namespace KPX.XivPlugin.DataModel.Shop

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb

open KPX.XivPlugin
open KPX.XivPlugin.DataModel

open LiteDB


[<CLIMutable>]
type GCScriptExchange =
    { [<BsonId>]
      LiteDbId: int
      Region: VersionRegion
      CostSeals: int
      ReceiveItem: int
      ReceiveQuantity: int }

type GCScriptShop private () =
    inherit CachedTableCollection<GCScriptExchange>()

    static member val Instance = GCScriptShop()

    override x.IsExpired = false

    override x.Depends = Array.empty

    override x.InitializeCollection() =
        x.DbCollection.EnsureIndex(fun x -> x.ReceiveItem) |> ignore

        seq {
            let col = ChinaDistroData.GetCollection()

            for row in col.GCScripShopItem.TypedRows do
                let key = row.Key.Main
                let item = row.Item.AsInt()

                if key >= 34 && item <> 0 then
                    let seals = row.``Cost{GCSeals}``.AsInt()

                    yield
                        { LiteDbId = 0
                          Region = VersionRegion.China
                          CostSeals = seals
                          ReceiveItem = item
                          ReceiveQuantity = 1 }


            let col = OfficalDistroData.GetCollection()

            for row in col.GCScripShopItem.TypedRows do
                let key = row.Key.Main
                let item = row.Item.AsInt()

                if key >= 34 && item <> 0 then
                    let seals = row.``Cost{GCSeals}``.AsInt()

                    yield
                        { LiteDbId = 0
                          Region = VersionRegion.Offical
                          CostSeals = seals
                          ReceiveItem = item
                          ReceiveQuantity = 1 }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.GetByItem(item: XivItem, region) =
        let itemId = item.ItemId
        x.DbCollection.Find(fun x -> x.ReceiveItem = itemId && x.Region = region)
