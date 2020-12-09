namespace BotData.EveData.EveType

open System
open System.IO
open System.Collections.Generic

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open BotData.Common.Database
open BotData.EveData.Group

[<CLIMutable>]
type EveType = 
    {
        [<LiteDB.BsonId(false)>]
        Id : int
        Name : string
        GroupId : int
        CategoryId : int
        Volume : float
        MetaGroupId : int
        /// 用途不明，可能是最小精炼单位
        PortionSize : int
        MarketGroupId : int
    }

type EveTypeCollection private () = 
    inherit CachedTableCollection<int, EveType>()

    static let instance = EveTypeCollection()

    static member Instance = instance

    override x.IsExpired = false

    override x.Depends = [| typeof<EveGroupCollection> |]

    override x.InitializeCollection() =
        x.DbCollection.EnsureIndex(LiteDB.BsonExpression.Create("Name")) |> ignore
        seq {
            use archive = 
                    let ResName = "BotData.EVEData.zip"
                    let assembly = Reflection.Assembly.GetExecutingAssembly()
                    let stream = assembly.GetManifestResourceStream(ResName)
                    new Compression.ZipArchive(stream, Compression.ZipArchiveMode.Read)
            use f = archive.GetEntry("evetypes.json").Open()
            use r = new JsonTextReader(new StreamReader(f))

            let groupCollection = EveGroupCollection.Instance

            let disallowCat = 
                [| 0; 1; 2; 11; 91;|]
                |> HashSet

            while r.Read() do 
                if r.TokenType = JsonToken.PropertyName then
                    r.Read() |> ignore
                    let o = JObject.Load(r)
                
                    let gid = o.GetValue("groupID").ToObject<int>()
                    let cat = groupCollection.[gid].CategoryId
                    let allow = disallowCat.Contains(cat) |> not

                    let tid = o.GetValue("typeID").ToObject<int>()
                    let mutable name = o.GetValue("typeName").ToObject<string>()

                    let vol = 
                        if o.ContainsKey("volume") then
                            o.GetValue("volume").ToObject<float>()
                        else
                            nan

                    let meta = 
                        if o.ContainsKey("metaGroupID") then
                            o.GetValue("metaGroupID").ToObject<int>()
                        else
                            1

                    let published = o.GetValue("published").ToObject<bool>()

                    let portionSize = 
                        if o.ContainsKey("portionSize") then
                            o.GetValue("portionSize").ToObject<int>()
                        else
                            0

                    let marketGroupId = 
                        if o.ContainsKey("marketGroupID") then
                            o.GetValue("marketGroupID").ToObject<int>()
                        else
                            0

                    if allow && published then
                        yield {
                            Id = tid
                            Name = name
                            GroupId = gid
                            CategoryId = cat
                            Volume = vol
                            MetaGroupId = meta
                            PortionSize = portionSize
                            MarketGroupId = marketGroupId
                        }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.Item (typeId : int) = x.GetById(typeId)

    member x.GetById(tid : int) =
        x.PassOrRaise(x.TryGetByKey(tid), "找不到物品:{0}", tid)

    member x.TryGetById(tid : int) = x.TryGetByKey(tid)

    member x.GetByName(name : string) =
        x.PassOrRaise(x.TryGetByName(name), "找不到物品:{0}", name) 

    member x.TryGetByName(name : string) =
        let bson = LiteDB.BsonValue(name)
        let ret = x.DbCollection.FindOne(LiteDB.Query.EQ("Name", bson))
        if isNull (box ret) then None else Some(ret)