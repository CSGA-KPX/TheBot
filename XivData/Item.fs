module XivData.Item
open System
open LibFFXIV.GameData.Raw

[<CLIMutable>]
type ItemRecord = 
    {
        [<LiteDB.BsonIdAttribute(false)>]
        Id   : int
        Name : string
    }

    override x.ToString() = 
        sprintf "%s(%i)" x.Name x.Id

    static member GetUnknown(lodeId) = 
        {
            Id      = -1
            Name = "未知"
        }

///部分重名道具列表
let internal itemOverriding = 
    [|
        {Id = 14928; Name = "长颈驼革手套";}    , {Id = 14928; Name = "长颈驼革手套时装";}
        {Id = 16604; Name = "鹰翼手铠";}        , {Id = 16604; Name = "鹰翼手铠时装";}
        {Id = 13741; Name = "管家之王证书";}    , {Id = 13741; Name = "过期的管家之王证书";}
        {Id = 18491; Name = "游牧御敌头盔";}    , {Id = 18491; Name = "游牧御敌头盔时装";}
        {Id = 17915; Name = "迦迦纳怪鸟的粗皮";}, {Id = 17915; Name = "大迦迦纳怪鸟的粗皮";}
        {Id = 20561; Name = "东方装甲";}        , {Id = 20561; Name = "东国装甲";}
        {Id = 24187; Name = "2018年度群狼盛宴区域锦标赛冠军之证";}        , {Id = 24187; Name = "2018年度群狼盛宴区域锦标赛冠军之证24187";}
        {Id = 24188; Name = "2018年度群狼盛宴区域锦标赛冠军之证";}        , {Id = 24188; Name = "2018年度群狼盛宴区域锦标赛冠军之证24188";}
        {Id = 24189; Name = "2018年度群狼盛宴区域锦标赛冠军之证";}        , {Id = 24189; Name = "2018年度群狼盛宴区域锦标赛冠军之证24189";}

    |] |> dict

type ItemCollection private () =
    inherit Utils.XivDataSource()
    let colName = "ItemRecord"
    let exists = Utils.Db.CollectionExists(colName)
    let db = Utils.Db.GetCollection<ItemRecord>(colName)
    do
        if not exists then
            //build from scratch
            let db = Utils.Db.GetCollection<ItemRecord>(colName)
            printfn "Building ItemCollection"
            db.EnsureIndex("_id", true) |> ignore
            db.EnsureIndex("Name") |> ignore
            let col = new LibFFXIV.GameData.Raw.XivCollection(XivLanguage.ChineseSimplified) :> IXivCollection
            let sht = col.GetSelectedSheet("Item", [|"Name"|])
            seq {
                for row in sht do
                    let row = row.Value
                    yield {Id = row.Key; Name = row.As<string>("Name")}
            } |> db.InsertBulk |> ignore
            GC.Collect()

    static let instance = new ItemCollection()
    static member Instance = instance

    member x.LookupByName(name : string) =
        let ret = db.FindOne(LiteDB.Query.EQ("Name", new LiteDB.BsonValue(name)))
        if isNull (box ret) then
            None
        else
            Some ret

    member x.LookupById(id : int) =
        let ret = db.FindById(new LiteDB.BsonValue(id))
        if isNull (box ret) then
            None
        else
            Some ret

    member x.SearchByName(str) = 
        db.Find(LiteDB.Query.Contains("Name", str))
        |> Seq.toArray

    member x.AllItems() = 
        db.FindAll() |> Seq.toArray