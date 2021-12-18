namespace KPX.XivPlugin.Data.Shops

open LiteDB

open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb

open KPX.XivPlugin.Data


[<CLIMutable>]
type GCScriptExchange =
    { [<BsonId(false)>]
      Id: int
      CostSeals: int
      ReceiveItem: int
      ReceiveQuantity: int }

type GCScriptShop private () =
    inherit CachedTableCollection<int, GCScriptExchange>()

    static let instance = GCScriptShop()

    static member Instance = instance

    override x.IsExpired = false

    override x.Depends = Array.empty

    override x.InitializeCollection() =
        x.DbCollection.EnsureIndex(BsonExpression.Create("ReceiveItem")) |> ignore

        let col = XivProvider.XivCollectionChs

        seq {
            for row in col.GCScripShopItem.TypedRows do
                let key = row.Key.Main
                let item = row.Item.AsInt()

                if key >= 34 && item <> 0 then
                    let seals = row.``Cost{GCSeals}``.AsInt()
                    let dbKey = row.Key.Main * 100 + row.Key.Alt

                    yield
                        { Id = dbKey
                          CostSeals = seals
                          ReceiveItem = item
                          ReceiveQuantity = 1 }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.GetByItem(item: XivItem) =
        x.DbCollection.Find(Query.EQ("ReceiveItem", BsonValue(item.Id)))
