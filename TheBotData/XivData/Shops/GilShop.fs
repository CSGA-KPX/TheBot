namespace KPX.TheBot.Data.XivData.Shops

open LiteDB

open KPX.TheBot.Data.Common.Database

open KPX.TheBot.Data.XivData


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
        use col = BotDataInitializer.XivCollectionChs

        col.GetSheet("Item", [| AskKey; BidKey |])
        |> ignore

        seq {
            for record in col.GetSheet("GilShopItem") do
                let item = record.AsRow("Item")

                yield
                    { Id = item.Key.Main
                      Ask = item.As<int32>(AskKey)
                      Bid = item.As<int32>(BidKey) }
        }
        |> Seq.distinctBy (fun x -> x.Id)
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.TryLookupByItem(item : XivItem) = x.TryGetByKey(item.Id)
