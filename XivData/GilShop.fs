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
    inherit Utils.XivDataSource()

    let colName = "GilShopCollection"
    let exists = Utils.Db.CollectionExists(colName)
    let db = Utils.Db.GetCollection<GilShopInfo>(colName)
    do
        if not exists then
            let db = Utils.Db.GetCollection<GilShopInfo>(colName)
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

    static let instance = new GilShopCollection()
    static member Instance = instance

    member x.LookupById(id : int) = 
        let ret = db.FindOne(Query.EQ("_id", new BsonValue(id)))
        if isNull (box(ret)) then
            None
        else
            Some(ret)

    member x.LookupByItem(item : Item.ItemRecord) =
        x.LookupById(item.Id)