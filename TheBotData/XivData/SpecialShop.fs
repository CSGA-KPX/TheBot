namespace KPX.TheBot.Data.XivData.SpecialShop

open System
open System.Collections.Generic

open LiteDB

open KPX.TheBot.Data.Common.Database

open KPX.TheBot.Data.XivData.Item

[<CLIMutable>]
type SpecialShopInfo =
    { [<BsonId(true)>]
      Id : int
      ReceiveItem : int32
      ReceiveCount : int32
      ReceiveHQ : bool
      CostItem : int32
      CostCount : int32 }

type SpecialShopCollection private () =
    inherit CachedTableCollection<int, SpecialShopInfo>()

    static let instance = SpecialShopCollection()
    static member Instance = instance

    override x.Depends = [| typeof<ItemCollection> |]

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection

        db.EnsureIndex(LiteDB.BsonExpression.Create("_id"), true)
        |> ignore

        db.EnsureIndex(LiteDB.BsonExpression.Create("ReceiveItem"))
        |> ignore

        use col = BotDataInitializer.XivCollectionChs
        let sht = col.GetSheet("SpecialShop")

        seq {
            for row in sht do
                let index prefix c p = sprintf "%s[%i][%i]" prefix c p

                for page = 0 to 1 do
                    for col = 0 to 59 do
                        let rItem =
                            row.AsRow(index "Item{Receive}" col page)

                        let cItem = row.As<int>(index "Item{Cost}" col page)

                        let r =
                            { Id = 0
                              ReceiveItem = rItem.Key.Main
                              ReceiveCount = row.As<int32>(index "Count{Receive}" col page)
                              ReceiveHQ = row.As<bool>(index "HQ{Receive}" col page)
                              CostItem = cItem
                              CostCount = row.As<int32>(index "Count{Cost}" col page) }

                        if rItem.Key.Main > 0
                           && r.ReceiveCount > 0
                           && cItem > 0
                           && r.ReceiveHQ = false
                           && rItem.As<bool>("IsUntradable") = false
                           && rItem.As<string>("Name") <> "" then
                            yield r
        }
        |> Seq.distinctBy (fun x -> sprintf "%i%i" x.ReceiveItem x.CostItem)
        |> db.InsertBulk
        |> ignore

    member x.AllCostItems() =
        let ic = ItemCollection.Instance

        x.DbCollection.FindAll()
        |> Seq.map (fun r -> r.CostItem)
        |> Seq.distinct
        |> Seq.map (fun id -> ic.GetByKey(id))
        |> Seq.toArray

    member x.SearchByCostItemId(id : int) =
        let ret =
            x.DbCollection.Find(Query.EQ("CostItem", BsonValue(id)))

        ret |> Seq.toArray
