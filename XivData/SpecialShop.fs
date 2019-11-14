module XivData.SpecialShop
open System
open System.Collections.Generic
open XivData.Item
open LiteDB
open LibFFXIV.GameData.Raw

[<CLIMutable>]
type SpecialShopInfo =    
    {
        [<BsonId(true)>]
        Id : int
        ReceiveItem  : int32
        ReceiveCount : uint32
        ReceiveHQ    : bool
        CostItem     : int32
        CostCount    : uint32
    }


type SpecialShopCollection private () =
    inherit Utils.XivDataSource()
    static let allowItemUICategory = 
        new HashSet<int>(
            [|
                yield 45
                yield! [47..54]
                yield 58
                yield 59
            |])
    let colName = "SpecialShopInfo"
    let exists = Utils.Db.CollectionExists(colName)
    let db = Utils.Db.GetCollection<SpecialShopInfo>(colName)
    do
        if not exists then
            //build from scratch
            let db = Utils.Db.GetCollection<SpecialShopInfo>(colName)
            printfn "Building SpecialShopInfo"
            db.EnsureIndex("_id", true) |> ignore
            db.EnsureIndex("ReceiveItem") |> ignore
            let col = new XivCollection(XivLanguage.ChineseSimplified) :> IXivCollection
            let sht = col.GetSheet("SpecialShop")
            seq {
                for row in sht do 
                    let index prefix c p = sprintf "%s[%i][%i]" prefix c p
                    for page = 0 to 1 do //不知道2是干嘛的，信息不全
                        for col = 0 to 59 do 
                            let rItem = row.AsRow(index "Item{Receive}" col page)
                            let r = 
                                {
                                    Id           = 0
                                    ReceiveItem  = rItem.Key.Main
                                    ReceiveCount = row.As<uint32>(index "Count{Receive}" col page)
                                    ReceiveHQ    = row.As<bool>(index "HQ{Receive}" col page)
                                    CostItem     = row.AsRaw(index "Item{Cost}" col page) |> int32
                                    CostCount    = row.As<uint32>(index "Count{Cost}" col page)
                                }
                            if rItem.Key.Main > 0
                            && r.ReceiveCount > 0u
                            && r.ReceiveHQ = false 
                            && rItem.As<bool>("IsUntradable") = false 
                            && allowItemUICategory.Contains(rItem.AsRaw("ItemUICategory") |> int) then
                                yield r
            }
            |> Seq.distinctBy (fun x -> sprintf "%i%i" x.ReceiveItem x.CostItem)
            |> db.InsertBulk |> ignore
            GC.Collect()

    static let instance = new SpecialShopCollection()
    static member Instance = instance

    member x.LookupByName(name : string) =
        let item = ItemCollection.Instance.LookupByName(name)
        if item.IsSome then
            Some (x.LookupById(item.Value.Id))
        else
            None

    member x.LookupById(id : int) =
        let ret = db.Find(Query.EQ("CostItem", new BsonValue(id)))
        ret |> Seq.toArray