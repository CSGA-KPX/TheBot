namespace KPX.EvePlugin.Data.EveMarketGroup

open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache


[<CLIMutable>]
type MarketGroup =
    { Id: int
      Name: string
      HasTypes: bool
      ParentGroupId: int }

type MarketGroupCollection private () =
    inherit CachedTableCollection<int, MarketGroup>()

    static let instance = MarketGroupCollection()

    static member Instance = instance

    override x.IsExpired = false

    override x.Depends = Array.empty

    override x.InitializeCollection() =
        x.DbCollection.EnsureIndex(LiteDB.BsonExpression.Create("Name")) |> ignore

        seq {
            use archive = KPX.EvePlugin.Data.Utils.GetEveDataArchive()

            use f = archive.GetEntry("marketgroups.json").Open()

            use r = new JsonTextReader(new StreamReader(f))

            while r.Read() do
                if r.TokenType = JsonToken.PropertyName then
                    let gid = r.Value :?> string |> int
                    r.Read() |> ignore
                    let o = JObject.Load(r)
                    let name = o.GetValue("name").ToObject<string>()

                    let has = o.GetValue("hasTypes").ToObject<int>() = 1

                    let pgid =
                        if o.ContainsKey("") then
                            o.GetValue("parentGroupID").ToObject<int>()
                        else
                            0

                    yield
                        { Id = gid
                          Name = name
                          HasTypes = has
                          ParentGroupId = pgid }
        }
        |> x.DbCollection.InsertBulk
        |> ignore


    member x.GetById(id: int) =
        x.PassOrRaise(x.DbCollection.TryFindById(id), "找不到市场类别{0}", id)

    member x.TryGetById(id: int) = x.DbCollection.TryFindById(id)

    member x.TryGetByName(name: string) =
        let bson = LiteDB.BsonValue(name)

        let ret = x.DbCollection.FindOne(LiteDB.Query.EQ("Name", bson))

        if isNull (box ret) then
            None
        else
            Some ret

    member x.GetByName(name) =
        x.PassOrRaise(x.TryGetByName(name), "找不到市场类别{0}", name)
