namespace KPX.EvePlugin.Data.Group

open System
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb


[<CLIMutable>]
type EveGroup =
    { [<LiteDB.BsonId(false)>]
      Id: int
      Name: string
      CategoryId: int
      IsPublished: bool }

type EveGroupCollection private () =
    inherit CachedTableCollection<int, EveGroup>()

    static let instance = EveGroupCollection()
    static member Instance = instance

    override x.IsExpired = false

    override x.Depends = Array.empty

    override x.InitializeCollection() =
        use archive = KPX.EvePlugin.Data.Utils.GetEveDataArchive()

        use f = archive.GetEntry("evegroups.json").Open()

        use r = new JsonTextReader(new StreamReader(f))

        seq {
            while r.Read() do
                if r.TokenType = JsonToken.PropertyName then
                    r.Read() |> ignore
                    let o = JObject.Load(r)

                    yield
                        { Id = o.GetValue("groupID").ToObject<int>()
                          Name = o.GetValue("groupName").ToObject<string>()
                          CategoryId = o.GetValue("categoryID").ToObject<int>()
                          IsPublished = o.GetValue("published").ToObject<bool>() }

        }
        |> x.DbCollection.InsertBulk
        |> ignore

    [<Obsolete>]
    member x.Item gid = x.DbCollection.TryFindById(gid)

    member x.GetByGroupId gid = x.DbCollection.SafeFindById(gid)
