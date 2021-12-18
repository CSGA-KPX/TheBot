namespace KPX.EvePlugin.Data.Process

open System.Collections.Generic
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache.LiteDb
open KPX.TheBot.Host.DataModel.Recipe

open KPX.EvePlugin.Data.EveType


/// 基于蓝图的生产配方数据库
///
/// 包含：一般蓝图和反应蓝图
type BlueprintCollection private () =
    inherit EveProcessCollection()

    static let instance = BlueprintCollection()

    static member Instance = instance

    member private x.TryReadProc(o: JObject) =
        let bpid = o.GetValue("blueprintTypeID").ToObject<int>()

        let a = o.GetValue("activities") :?> JObject

        if a.ContainsKey("manufacturing") || a.ContainsKey("reaction") then
            let m, procType =
                if a.ContainsKey("manufacturing") then
                    a.GetValue("manufacturing") :?> JObject, ProcessType.Manufacturing
                elif a.ContainsKey("reaction") then
                    a.GetValue("reaction") :?> JObject, ProcessType.Reaction
                else
                    failwithf ""

            let input, output = List<EveDbMaterial>(), List<EveDbMaterial>()
            // quantity * typeID
            if m.ContainsKey("materials") then
                for item in m.GetValue("materials") :?> JArray do
                    let item = item :?> JObject

                    input.Add(
                        { Item = item.GetValue("typeID").Value<int>()
                          Quantity = item.GetValue("quantity").Value<float>() }
                    )

            if m.ContainsKey("products") then
                for item in m.GetValue("products") :?> JArray do
                    let item = item :?> JObject

                    output.Add(
                        { Item = item.GetValue("typeID").Value<int>()
                          Quantity = item.GetValue("quantity").Value<float>() }
                    )

            let ec = EveTypeCollection.Instance

            if output.Count = 1
               && ec.TryGetById(bpid).IsSome
               && ec.TryGetById(output.[0].Item).IsSome then
                Some
                    { Id = bpid
                      Process =
                          { Input = input.ToArray()
                            Output = output.ToArray() }
                      Type = procType }
            else
                None
        else
            None


    override x.InitializeCollection() =
        let expr = LiteDB.BsonExpression.Create("Process.Output[0].Item")

        x.DbCollection.EnsureIndex("ProductId", expr) |> ignore

        seq {
            use archive = KPX.EvePlugin.Data.Utils.GetEveDataArchive()

            use f = archive.GetEntry("blueprints.json").Open()

            use r = new JsonTextReader(new StreamReader(f))

            while r.Read() do
                if r.TokenType = JsonToken.PropertyName then
                    r.Read() |> ignore
                    let proc = x.TryReadProc(JObject.Load(r))
                    if proc.IsSome then yield proc.Value
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.GetAllProcesses() =
        x.DbCollection.FindAll() |> Seq.map (fun proc -> proc.AsEveProcess())

    interface IRecipeProvider<EveType, EveProcess> with

        /// item可以是蓝图或者产物
        override x.TryGetRecipe(item) =

            let internalProc =
                let tryAsBp = x.DbCollection.TryFindById(item.Id)

                if tryAsBp.IsSome then
                    tryAsBp
                else
                    let id = LiteDB.BsonValue(item.Id)

                    let ret = x.DbCollection.FindOne(LiteDB.Query.EQ("Process.Output[0].Item", id))

                    if isNull (box ret) then
                        None
                    else
                        Some ret

            internalProc |> Option.map (fun proc -> proc.AsEveProcess())
