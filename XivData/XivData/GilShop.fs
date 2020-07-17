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
      Ask : uint32
      Bid : uint32 }


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
        printfn "Building GilShopCollection"
        db.EnsureIndex("_id", true) |> ignore
        let col = BotDataInitializer.GetXivCollectionChs()

        //用于缓存
        col.GetSheet("Item", [| AskKey; BidKey |])
        |> ignore
        seq {
            for record in col.GetSheet("GilShopItem") do
                let item = record.AsRow("Item")
                yield { Id = item.Key.Main
                        Ask = item.As<uint32>(AskKey)
                        Bid = item.As<uint32>(BidKey) }
        }
        |> Seq.distinctBy (fun x -> x.Id)
        |> db.InsertBulk
        |> ignore

    member x.TryLookupByItem(item : ItemRecord) = x.TryGetByKey(item.Id)
