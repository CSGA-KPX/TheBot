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
      PatchNumber: PatchNumber
      ReceiveItem: int32
      ReceiveCount: int32
      ReceiveHQ: bool
      CostItem: int32
      CostCount: int32 }

type SpecialShopCollection private () =
    inherit CachedTableCollection<SpecialShopInfo>()

    static let currencies = [ 1, 28; 2, 25519; 4, 25200; 6, 33913; 7, 33914 ] |> readOnlyDict

    static member val Instance = SpecialShopCollection()

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        x.DbCollection.EnsureIndex(fun x -> x.ReceiveItem) |> ignore

        x.InitChs()
        x.InitOffical()

    member private x.InitChs() =
        let col = ChinaDistroData.GetCollection()
        let version = VersionRegion.China

        seq {
            let existed = System.Collections.Generic.HashSet<string>()

            let tomestones =
                seq {
                    for row in col.TomestonesItem.TypedRows do
                        row.Key.Main, row.Item.AsInt()
                }
                |> readOnlyDict

            for row in col.SpecialShop.TypedRows do
                let rItem = row.``Item{Receive}``.AsRows()
                let rCount = row.``Count{Receive}``.AsInts()
                let rHq = row.``HQ{Receive}``.AsBools()
                let cItem = row.``Item{Cost}``.AsInts()
                let cCount = row.``Count{Cost}``.AsInts()

                for i = rItem.GetLowerBound(0) to rItem.GetUpperBound(0) do
                    for j = rItem.GetLowerBound(1) to rItem.GetUpperBound(1) do
                        let key = $"%i{rItem.[i, j].Key.Main}%i{cItem.[i, j]}"
                        let patch = ItemPatchDifference.ToPatchNumber(rItem.[i, j].Key.Main)

                        if not <| (existed.Contains(key))
                           && cItem.[i, j] > 0
                           && rItem.[i, j].Key.Main > 0
                           && rCount.[i, j] > 0
                           && rHq.[i, j] = false
                           && rItem.[i, j].IsUntradable.AsBool() = false
                           && rItem.[i, j].Name.AsString() <> ""
                           && patch <> PatchNumber.Patch_Invalid then
                            existed.Add(key) |> ignore

                            // fix from
                            // https://github.com/xivapi/SaintCoinach/
                            // blob/fabeedb29921358fe3025c65e72a3de14a3c3070/
                            // SaintCoinach/Xiv/SpecialShopListing.cs
                            let costItem =
                                let mutable cItem = cItem.[i, j]

                                if cItem < 8 then
                                    match row.UseCurrencyType.AsInt() with
                                    // 货币
                                    | 16 -> cItem <- currencies.[cItem]
                                    // 物品
                                    | 8 -> ()
                                    // 神典石
                                    | 4 -> cItem <- tomestones.[cItem]
                                    | _ -> invalidArg "UseCurrencyType" "非法值"

                                cItem

                            yield
                                { LiteDbId = 0
                                  Region = version
                                  PatchNumber = patch
                                  ReceiveItem = rItem.[i, j].Key.Main
                                  ReceiveCount = rCount.[i, j]
                                  ReceiveHQ = rHq.[i, j]
                                  CostItem = costItem
                                  CostCount = cCount.[i, j] }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member private x.InitOffical() =
        let col = OfficalDistroData.GetCollection()
        let version = VersionRegion.Offical

        seq {
            let existed = System.Collections.Generic.HashSet<string>()

            let tomestones =
                seq {
                    for row in col.TomestonesItem.TypedRows do
                        row.Key.Main, row.Item.AsInt()
                }
                |> readOnlyDict

            for row in col.SpecialShop.TypedRows do
                let rItem = row.``Item{Receive}``.AsRows()
                let rCount = row.``Count{Receive}``.AsInts()
                let rHq = row.``HQ{Receive}``.AsBools()
                let cItem = row.``Item{Cost}``.AsInts()
                let cCount = row.``Count{Cost}``.AsInts()

                for i = rItem.GetLowerBound(0) to rItem.GetUpperBound(0) do
                    for j = rItem.GetLowerBound(1) to rItem.GetUpperBound(1) do
                        let key = $"%i{rItem.[i, j].Key.Main}%i{cItem.[i, j]}"
                        let patch = ItemPatchDifference.ToPatchNumber(rItem.[i, j].Key.Main)

                        if not <| (existed.Contains(key))
                           && cItem.[i, j] > 0
                           && rItem.[i, j].Key.Main > 0
                           && rCount.[i, j] > 0
                           && rHq.[i, j] = false
                           && rItem.[i, j].IsUntradable.AsBool() = false
                           && rItem.[i, j].Name.AsString() <> ""
                           && patch <> PatchNumber.Patch_Invalid then
                            existed.Add(key) |> ignore

                            // fix from
                            // https://github.com/xivapi/SaintCoinach/
                            // blob/fabeedb29921358fe3025c65e72a3de14a3c3070/
                            // SaintCoinach/Xiv/SpecialShopListing.cs
                            let costItem =
                                let mutable cItem = cItem.[i, j]

                                if cItem < 8 then
                                    match row.UseCurrencyType.AsInt() with
                                    // 货币
                                    | 16 -> cItem <- currencies.[cItem]
                                    // 物品
                                    | 8 -> ()
                                    // 神典石
                                    | 4 -> cItem <- tomestones.[cItem]
                                    | _ -> invalidArg "UseCurrencyType" "非法值"

                                cItem

                            yield
                                { LiteDbId = 0
                                  Region = version
                                  PatchNumber = patch
                                  ReceiveItem = rItem.[i, j].Key.Main
                                  ReceiveCount = rCount.[i, j]
                                  ReceiveHQ = rHq.[i, j]
                                  CostItem = costItem
                                  CostCount = cCount.[i, j] }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.AllCostItems() =
        let ic = ItemCollection.Instance

        x.DbCollection.FindAll()
        |> Seq.map (fun r -> r.CostItem)
        |> Seq.distinct
        |> Seq.map (fun id -> ic.GetByItemId(id))
        |> Seq.toArray

    member x.SearchByCostItem(item: XivItem, region: VersionRegion) =
        Query.And(Query.EQ("CostItem", item.ItemId), Query.EQ("Region", region.BsonValue))
        |> x.DbCollection.Find
        |> Seq.toArray

    member x.SearchByCostItem(item: XivItem, world: World) =
        x.SearchByCostItem(item, world.VersionRegion)
