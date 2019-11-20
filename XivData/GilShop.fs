module XivData.GilShop
open System
open System.Collections.Generic
open XivData.Item
open LiteDB
open LibFFXIV.GameData.Raw

[<Literal>]
let AskKey = "Price{Mid}"

[<Literal>]
let BidKey = "Price{Low}"

[<CLIMutable>]
type GilShopInfo =    
    {
        [<BsonId(true)>]
        Id  : int
        Ask : uint32
        Bid : uint32
    }


type GilShopCollection private () = 
    inherit Utils.XivDataSource<int, GilShopInfo>()

    static let instance = new GilShopCollection()
    static member Instance = instance

    override x.BuildCollection() = 
        let db = x.Collection
        printfn "Building GilShopCollection"
        db.EnsureIndex("_id", true) |> ignore
        let col = new XivCollection(XivLanguage.ChineseSimplified) :> IXivCollection
        //用于缓存
        col.GetSheet("Item", [|AskKey; BidKey|]) |> ignore
        seq {
            for record in col.GetSheet("GilShopItem") do 
                let item = record.AsRow("Item")
                yield {
                    Id = item.Key.Main
                    Ask = item.As<uint32>(AskKey)
                    Bid = item.As<uint32>(BidKey)
                }
        }
        |> Seq.distinctBy (fun x -> x.Id)
        |> db.InsertBulk |> ignore
        GC.Collect()

    member x.TryLookupByItem(item : Item.ItemRecord) =
        x.TryLookupById(item.Id)