namespace BotData.XivData.SpecialShop

open System
open System.Collections.Generic

open LiteDB

open BotData.Common.Database

open BotData.XivData.Item

[<CLIMutable>]
type SpecialShopInfo =
    { [<BsonId(true)>]
      Id : int
      ReceiveItem : int32
      ReceiveCount : uint32
      ReceiveHQ : bool
      CostItem : int32
      CostCount : uint32 }

type SpecialShopCollection private () =
    inherit CachedTableCollection<int, SpecialShopInfo>()

    static let allowItemUICategory =
        HashSet<int>([| yield 33
                        yield 45
                        yield! [ 47 .. 54 ]
                        yield 58
                        yield 59 |])

    static let instance = SpecialShopCollection()
    static member Instance = instance

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection
        
        db.EnsureIndex("_id", true) |> ignore
        db.EnsureIndex("ReceiveItem") |> ignore
        let col = BotDataInitializer.GetXivCollectionChs()
        let sht = col.GetSheet("SpecialShop")
        seq {
            for row in sht do
                let index prefix c p = sprintf "%s[%i][%i]" prefix c p
                for page = 0 to 1 do //不知道2是干嘛的，信息不全
                    for col = 0 to 59 do
                        let rItem = row.AsRow(index "Item{Receive}" col page)

                        let r =
                            { Id = 0
                              ReceiveItem = rItem.Key.Main
                              ReceiveCount = row.As<uint32>(index "Count{Receive}" col page)
                              ReceiveHQ = row.As<bool>(index "HQ{Receive}" col page)
                              CostItem = row.As<int>(index "Item{Cost}" col page)
                              CostCount = row.As<uint32>(index "Count{Cost}" col page) }
                        if rItem.Key.Main > 0 && r.ReceiveCount > 0u && r.ReceiveHQ = false
                           && rItem.As<bool>("IsUntradable") = false then yield r
                           //&& allowItemUICategory.Contains(rItem.As<int>("ItemUICategory")) then yield r
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
        let ret = x.DbCollection.Find(Query.EQ("CostItem", BsonValue(id)))
        ret |> Seq.toArray