namespace KPX.XivPlugin.Data.Shops

open LiteDB

open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb

open KPX.XivPlugin.Data


[<CLIMutable>]
type SpecialShopInfo =
    { [<BsonId(true)>]
      Id: int
      ReceiveItem: int32
      ReceiveCount: int32
      ReceiveHQ: bool
      CostItem: int32
      CostCount: int32 }

type SpecialShopCollection private () =
    inherit CachedTableCollection<int, SpecialShopInfo>()

    static let instance = SpecialShopCollection()
    static member Instance = instance

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        x.DbCollection.EnsureIndex(BsonExpression.Create("ReceiveItem")) |> ignore

        let col = XivProvider.XivCollectionChs

        //col.GetSheet("Item", [| "Name"; "IsUntradable" |])
        //|> ignore // 缓存

        seq {
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
                                { Id = 0
                                  ReceiveItem = rItem.[i, j].Key.Main
                                  ReceiveCount = rCount.[i, j]
                                  ReceiveHQ = rHq.[i, j]
                                  CostItem = cItem.[i, j]
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

    member x.SearchByCostItemId(id: int) =
        let ret = x.DbCollection.Find(Query.EQ("CostItem", BsonValue(id)))

        ret |> Seq.toArray
