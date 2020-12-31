namespace KPX.TheBot.Data.XivData.Shops

open System

open LiteDB

open KPX.TheBot.Data.Common.Database
open KPX.TheBot.Data.XivData


[<CLIMutable>]
type GCScriptExchange =
    { [<BsonId(false)>]
      Id : int
      CostSeals : int
      ReceiveItem : int
      ReceiveQuantity : int }

type GCScriptShop private () =
    inherit CachedTableCollection<int, GCScriptExchange>()

    static let instance = GCScriptShop()

    static member Instance = instance

    override x.IsExpired = false

    override x.Depends = Array.empty

    override x.InitializeCollection() =
        use col = BotDataInitializer.XivCollectionChs
        x.DbCollection.EnsureIndex(LiteDB.BsonExpression.Create("ReceiveItem"))
        |> ignore

        seq {
            for row in col.GetSheet("GCScripShopItem") do
                let key = row.Key.Main
                let item = row.As<int>("Item")

                if key >= 34 && item <> 0 then
                    let seals = row.As<int>("Cost{GCSeals}")
                    let dbKey = row.Key.Main * 100 + row.Key.Alt

                    yield
                        { Id = dbKey
                          CostSeals = seals
                          ReceiveItem = item
                          ReceiveQuantity = 1 }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.GetByItem(item : XivItem) = 
        x.DbCollection.Find(Query.EQ("ReceiveItem", BsonValue(item.Id)))