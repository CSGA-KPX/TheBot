module XivData.SpecialShop

open System
open System.Collections.Generic
open XivData.Item
open LiteDB
open LibFFXIV.GameData.Raw

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
    inherit Utils.XivDataSource<int, SpecialShopInfo>()

    static let allowItemUICategory =
        HashSet<int>([| yield 45
                        yield! [ 47 .. 54 ]
                        yield 58
                        yield 59 |])

    static let instance = SpecialShopCollection()
    static member Instance = instance

    override x.BuildCollection() =
        let db = x.Collection
        printfn "Building SpecialShopInfo"
        db.EnsureIndex("_id", true) |> ignore
        db.EnsureIndex("ReceiveItem") |> ignore
        let col = EmbeddedXivCollection(XivLanguage.ChineseSimplified) :> IXivCollection
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
                           && rItem.As<bool>("IsUntradable") = false
                           && allowItemUICategory.Contains(rItem.As<int>("ItemUICategory")) then yield r
        }
        |> Seq.distinctBy (fun x -> sprintf "%i%i" x.ReceiveItem x.CostItem)
        |> db.InsertBulk
        |> ignore
        GC.Collect()

    member x.SearchByCostItemId(id : int) =
        let ret = x.Collection.Find(Query.EQ("CostItem", BsonValue(id)))
        ret |> Seq.toArray

    member x.TrySearchByName(name : string) =
        let item = ItemCollection.Instance.TryLookupByName(name)
        if item.IsSome then Some(x.SearchByCostItemId(item.Value.Id))
        else None
