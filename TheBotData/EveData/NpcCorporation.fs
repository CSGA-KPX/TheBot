namespace BotData.EveData.NpcCorporation

open System
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open BotData.Common.Database

[<CLIMutable>]
type NpcCorporation =
    {
        [<LiteDB.BsonId(false)>]
        Id : int
        CorporationName : string
    }

type NpcCorporationoCollection private () = 
    inherit CachedTableCollection<int, NpcCorporation>()

    static let instance = NpcCorporationoCollection()

    static member Instance = instance

    override x.IsExpired = false

    override x.Depends = Array.empty

    override x.InitializeCollection() =
        x.DbCollection.EnsureIndex(LiteDB.BsonExpression.Create("CorporationName")) |> ignore

        seq {
            use archive = BotData.EveData.Utils.GetEveDataArchive()
            use f = archive.GetEntry("npccorporations.json").Open()
            use r = new JsonTextReader(new StreamReader(f))

            while r.Read() do 
                if r.TokenType = JsonToken.PropertyName then
                    let cid = r.Value :?> string |> int
                    r.Read() |> ignore
                    let o = JObject.Load(r)
                    let cname = o.GetValue("name").Value<string>()
                    yield {Id = cid; CorporationName = cname}
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.GetByName(name : string)= 
        x.PassOrRaise(x.TryGetByName(name), "找不到军团:{0}", name)

    member x.TryGetByName(name : string)= 
        let bson = LiteDB.BsonValue(name)
        let ret = x.DbCollection.FindOne(LiteDB.Query.EQ("CorporationName", bson))
        if isNull (box ret) then None else Some ret