namespace BotData.XivData.GilShop

open System
open System.Collections.Generic

open LiteDB

open BotData.Common.Database

open BotData.XivData.Item

[<CLIMutable>]
type GilShopInfo =
    { [<BsonId(true)>]
      Id : int
      Ask : int32
      Bid : int32 }


type GilShopCollection private () =
    inherit CachedTableCollection<int, GilShopInfo>()

    static let AskKey = "Price{Mid}"
    static let BidKey = "Price{Low}"

    static let instance = GilShopCollection()
    static member Instance = instance

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection
        db.EnsureIndex(LiteDB.BsonExpression.Create("_id"), true) |> ignore

        use col = BotDataInitializer.XivCollectionChs
        col.GetSheet("Item", [| AskKey; BidKey |])
        |> ignore
        seq {
            for record in col.GetSheet("GilShopItem") do
                let item = record.AsRow("Item")
                yield { Id = item.Key.Main
                        Ask = item.As<int32>(AskKey)
                        Bid = item.As<int32>(BidKey) }
        }
        |> Seq.distinctBy (fun x -> x.Id)
        |> db.InsertBulk
        |> ignore

    member x.TryLookupByItem(item : ItemRecord) = x.TryGetByKey(item.Id)
