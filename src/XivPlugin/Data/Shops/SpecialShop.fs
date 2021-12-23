namespace KPX.XivPlugin.Data.Shop

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb

open KPX.XivPlugin
open KPX.XivPlugin.Data

open LiteDB


[<CLIMutable>]
type SpecialShopInfo =
    { [<BsonId>]
      LiteDbId: int
      Region: VersionRegion
      ReceiveItem: int32
      ReceiveCount: int32
      ReceiveHQ: bool
      CostItem: int32
      CostCount: int32 }

type SpecialShopCollection private () =
    inherit CachedTableCollection<SpecialShopInfo>()

    static member val Instance = SpecialShopCollection()

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        x.DbCollection.EnsureIndex(fun x -> x.ReceiveItem) |> ignore

        x.InitChs()
        x.InitOffical()

    member private x.InitChs() =
        seq {
            let col = ChinaDistroData.GetCollection()
            let existed = System.Collections.Generic.HashSet<string>()

            for row in col.SpecialShop.TypedRows do
                let rItem = row.``Item{Receive}``.AsRows()
                let rCount = row.``Count{Receive}``.AsInts()
                let rHq = row.``HQ{Receive}``.AsBools()

                let cItem = row.``Item{Cost}``.AsInts()
                let cCount = row.``Count{Cost}``.AsInts()

                for i = rItem.GetLowerBound(0) to rItem.GetUpperBound(0) do
                    for j = rItem.GetLowerBound(1) to rItem.GetUpperBound(1) do
                        let key = $"%i{rItem.[i, j].Key.Main}%i{cItem.[i, j]}"

                        if not <| (existed.Contains(key))
                           && cItem.[i, j] > 0
                           && rItem.[i, j].Key.Main > 0
                           && rCount.[i, j] > 0
                           && rHq.[i, j] = false
                           && rItem.[i, j].IsUntradable.AsBool() = false
                           && rItem.[i, j].Name.AsString() <> "" then
                            existed.Add(key) |> ignore

                            yield
                                { LiteDbId = 0
                                  Region = VersionRegion.China
                                  ReceiveItem = rItem.[i, j].Key.Main
                                  ReceiveCount = rCount.[i, j]
                                  ReceiveHQ = rHq.[i, j]
                                  CostItem = cItem.[i, j]
                                  CostCount = cCount.[i, j] }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member private x.InitOffical() =
        seq {
            let col = OfficalDistroData.GetCollection()
            let existed = System.Collections.Generic.HashSet<string>()

            for row in col.SpecialShop.TypedRows do
                let rItem = row.``Item{Receive}``.AsRows()
                let rCount = row.``Count{Receive}``.AsInts()
                let rHq = row.``HQ{Receive}``.AsBools()

                let cItem = row.``Item{Cost}``.AsInts()
                let cCount = row.``Count{Cost}``.AsInts()

                for i = rItem.GetLowerBound(0) to rItem.GetUpperBound(0) do
                    for j = rItem.GetLowerBound(1) to rItem.GetUpperBound(1) do
                        let key = $"%i{rItem.[i, j].Key.Main}%i{cItem.[i, j]}"

                        if not <| (existed.Contains(key))
                           && cItem.[i, j] > 0
                           && rItem.[i, j].Key.Main > 0
                           && rCount.[i, j] > 0
                           && rHq.[i, j] = false
                           && rItem.[i, j].IsUntradable.AsBool() = false
                           && rItem.[i, j].Name.AsString() <> "" then
                            existed.Add(key) |> ignore

                            yield
                                { LiteDbId = 0
                                  Region = VersionRegion.Offical
                                  ReceiveItem = rItem.[i, j].Key.Main
                                  ReceiveCount = rCount.[i, j]
                                  ReceiveHQ = rHq.[i, j]
                                  CostItem = cItem.[i, j]
                                  CostCount = cCount.[i, j] }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.AllCostItems(region) =
        let ic = ItemCollection.Instance

        x.DbCollection.FindAll()
        |> Seq.map (fun r -> r.CostItem)
        |> Seq.distinct
        |> Seq.map (fun id -> ic.GetByItemId(id, region))
        |> Seq.toArray

    member x.SearchByCostItem(item: XivItem, region) =
        let itemId = item.ItemId
        x.DbCollection.QueryAllArray(fun x -> x.CostItem = itemId && x.Region = region)
