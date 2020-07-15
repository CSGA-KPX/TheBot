namespace BotData.EveData.NpcCorporation

open System
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open BotData.Common.Database

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
        x.DbCollection.EnsureIndex("CorporationName") |> ignore

        seq {
            use archive = 
                    let ResName = "BotData.EVEData.zip"
                    let assembly = Reflection.Assembly.GetExecutingAssembly()
                    let stream = assembly.GetManifestResourceStream(ResName)
                    new IO.Compression.ZipArchive(stream, Compression.ZipArchiveMode.Read)
            use f = archive.GetEntry("crpnpccorporations.json").Open()
            use r = new JsonTextReader(new StreamReader(f))

            for item in JArray.Load(r) do 
                let item = item :?> JObject
                let cid = item.GetValue("corporationID").ToObject<int>()
                let cname = item.GetValue("corporationName").ToObject<string>()
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