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
    inherit CachedTableCollection<int, GilShopInfo>(DefaultDB)

    static let instance = GilShopCollection()
    static member Instance = instance

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let col = BotDataInitializer.XivCollectionChs

        // col.GetSheet("Item", [| AskKey; BidKey |])

        seq {
            for row in col.GilShopItem.TypedRows do
                let item = row.Item.AsRow()

                yield
                    { Id = item.Key.Main
                      Ask = item.``Price{Mid}``.AsInt()
                      Bid = item.``Price{Low}``.AsInt() }
        }
        |> Seq.distinctBy (fun x -> x.Id)
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.TryLookupByItem(item : XivItem) = x.DbCollection.TryFindById(item.Id)
