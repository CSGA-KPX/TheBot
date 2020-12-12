namespace BotData.EveData.Process

open System
open System.Collections.Generic
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open BotData.CommonModule.Recipe
open BotData.EveData.EveType


/// 行星材料配方
type PlanetProcessCollection private () = 
    inherit EveProcessCollection()
    
    static let instance = PlanetProcessCollection()

    static member Instance = instance

    override x.InitializeCollection() = 
        let expr = LiteDB.BsonExpression.Create("Process.Output[0].Item")
        x.DbCollection.EnsureIndex("ProductId", expr) |> ignore

        use archive = BotData.EveData.Utils.GetEveDataArchive()
        use f = archive.GetEntry("planetschematicstypemap.json").Open()
        use r = new JsonTextReader(new StreamReader(f))

        // reactionTypeID * EveBlueprint
        let dict = Dictionary<int, {| Id : int
                                      In : List<EveDbMaterial>
                                      Out : List<EveDbMaterial> |}>()

        for item in JArray.Load(r) do 
            let item = item  :?> JObject
            let sid     = (item.GetValue("schematicID").ToObject<int>())
            if not <| dict.ContainsKey(sid) then
                dict.[sid] <- {|Id = sid; In = List<EveDbMaterial>(); Out = List<EveDbMaterial>()|}

            let q       = item.GetValue("quantity").ToObject<float>()
            let tid     = item.GetValue("typeID").ToObject<int>()
            let em = {Item = tid; Quantity = q}

            if item.GetValue("isInput").ToObject<int>() = 1 then
                dict.[sid].In.Add(em)
            else
                dict.[sid].Out.Add(em)

        let ec = EveTypeCollection.Instance
        dict.Values
        |> Seq.filter (fun temp ->
            // 屏蔽P0->P1生产过程
            // 1042 = P1
            let gid = ec.GetById(temp.Out.[0].Item).GroupId
            gid <> 1042)
        |> Seq.map (fun temp -> { Id = temp.Id
                                  Process = {Input = temp.In.ToArray()
                                             Output = temp.Out.ToArray()}
                                  Type = ProcessType.Planet})
        |> x.DbCollection.InsertBulk
        |> ignore

    interface IRecipeProvider<EveType, EveProcess> with
        override x.TryGetRecipe(item) =
            let id = new LiteDB.BsonValue(item.Id)
            let ret = x.DbCollection.FindOne(LiteDB.Query.EQ("Process.Output[0].Item", id))
            if isNull (box ret) then
                None
            else
                Some (ret.AsEveProcess())