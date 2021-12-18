namespace KPX.EvePlugin.Data.Process

open System.Collections.Generic
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open KPX.TheBot.Host.DataCache.LiteDb
open KPX.TheBot.Host.DataModel.Recipe

open KPX.EvePlugin.Data.EveType


/// 行星材料配方
type PlanetProcessCollection private () =
    inherit EveProcessCollection()

    static let instance = PlanetProcessCollection()

    static member Instance = instance

    override x.InitializeCollection() =
        let expr = LiteDB.BsonExpression.Create("Process.Output[0].Item")

        x.DbCollection.EnsureIndex("ProductId", expr) |> ignore

        use archive = KPX.EvePlugin.Data.Utils.GetEveDataArchive()

        use f = archive.GetEntry("schematics.json").Open()

        use r = new JsonTextReader(new StreamReader(f))

        let ec = EveTypeCollection.Instance

        let input = List<EveDbMaterial>()
        let output = List<EveDbMaterial>()

        seq {
            while r.Read() do
                if r.TokenType = JsonToken.PropertyName then
                    input.Clear()
                    output.Clear()

                    let sid = r.Value :?> string |> int
                    r.Read() |> ignore
                    let o = JObject.Load(r)
                    let types = o.GetValue("types") :?> JObject

                    for p in types.Properties() do
                        let id = p.Name |> int
                        let quantity = p.Value.Value<float>("quantity")
                        let m = { Item = id; Quantity = quantity }
                        let isInput = p.Value.Value<bool>("isInput")

                        if isInput then
                            input.Add(m)
                        else
                            output.Add(m)

                    let pid = output.[0].Item
                    let gid = ec.GetById(pid).GroupId
                    let notP0ToP1 = gid <> 1042

                    if notP0ToP1 then
                        yield
                            { Id = sid
                              Process =
                                  { Input = input.ToArray()
                                    Output = output.ToArray() }
                              Type = ProcessType.Planet }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    interface IRecipeProvider<EveType, EveProcess> with
        override x.TryGetRecipe(item) =
            let id = LiteDB.BsonValue(item.Id)

            let ret = x.DbCollection.FindOne(LiteDB.Query.EQ("Process.Output[0].Item", id))

            if isNull (box ret) then
                None
            else
                Some(ret.AsEveProcess())
