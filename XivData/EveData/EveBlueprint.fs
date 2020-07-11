namespace BotData.EveData.EveBlueprint

open System
open System.IO
open System.Collections.Generic

open LiteDB

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open BotData.Common.Database

open BotData.EveData.Utils
open BotData.EveData.EveType

type BlueprintType = 
    | Unknown = 0
    | Manufacturing = 1
    | Planet = 2
    | Reaction = 3

type EveBlueprint = 
    {
        [<LiteDB.BsonId(false)>]
        Id : int
        Materials : EveMaterial []
        Products : EveMaterial []
        Type : BlueprintType
    }

    static member Default =
        {
            Id = Int32.MinValue
            Materials = Array.empty
            Products = Array.empty
            Type = BlueprintType.Unknown
        }

    /// 计算所需流程数的材料，在计算材料效率后使用
    /// 
    /// 结果会ceil
    member x.GetBpByRuns(r : float) = 
        let ms = 
            x.Materials
            |> Array.map (fun m -> {m with Quantity = m.Quantity * r |> ceil})
        let ps = 
            x.Products
            |> Array.map (fun p -> {p with Quantity = p.Quantity * r |> ceil})

        {x with Materials = ms; Products = ps}

    /// 按产出量计算所需材料，在计算材料效率后使用
    ///
    /// 细节见 https://github.com/CSGA-KPX/TheBot/issues/7
    member x.GetBpByItemNoCeil(q : float) = 
        let runs = q / x.ProductQuantity
        let oneRun = x.GetBpByRuns(1.0)
        let ms = 
            oneRun.Materials
            |> Array.map (fun m -> {m with Quantity = m.Quantity * runs})

        let ps = 
            oneRun.Products
            |> Array.map (fun p -> {p with Quantity = p.Quantity * runs})

        {oneRun with Materials = ms; Products = ps}

    /// 根据材料效率调整材料数量，结果向上取整(ceil)
    ///
    /// 在计算流程后使用
    ///
    /// 仅对“制造”蓝图有效，其他蓝图直接返回
    member x.ApplyMaterialEfficiency(me : int) =
        if x.Type = BlueprintType.Manufacturing then
            let factor = (100 - me) |> pct
            let ms = 
                x.Materials
                |> Array.map (fun m -> 
                    let q = (float m.Quantity) * factor |> ceil
                    {m with Quantity = q}
                )
            {x with Materials = ms}
        else
            x

    member private x.FailOnMultipleProducts() = 
        if x.Products.Length > 1 then
            failwithf "当前产品数多于1个，%A" x

    /// 仅有一个产品时返回材料Id，其他则抛出异常
    member x.ProductId =
        x.FailOnMultipleProducts()
        (x.Products |> Array.head).TypeId

    /// 仅有一个产品时返回材料数量，其他则抛出异常
    member x.ProductQuantity =
        x.FailOnMultipleProducts()
        (x.Products |> Array.head).Quantity

type EveBlueprintCollection private () = 
    inherit CachedTableCollection<int, EveBlueprint>()

    static let instance = EveBlueprintCollection()

    static member Instance = instance

    override x.IsExpired = false

    override x.Depends = [| typeof<EveTypeCollection> |]

    override x.InitializeCollection() =
        // 产物索引
        x.DbCollection.EnsureIndex("ProductId", "$.Products[0].TypeId") |> ignore
        seq {
            yield! x.InitPlanetSchematics()
            let ec = EveTypeCollection.Instance
            for bp in x.InitBlueprints() do 
                // 检查蓝图信息
                if bp.Products.Length = 1 && (ec.TryGetById(bp.Id).IsSome) then
                    yield bp
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.TryGetByProduct(id : int) = 
        let bson = LiteDB.BsonValue(id)
        let ret = x.DbCollection.FindOne(LiteDB.Query.EQ("ProductId", bson))
        if isNull(box ret) then None else Some(ret)
        

    member x.TryGetByProduct(t : EveType) = x.TryGetByProduct(t.Id)

    member x.GetByProduct(t : EveType) = x.TryGetByProduct(t).Value
    member x.GetByProduct(id : int) = x.TryGetByProduct(id).Value

    member x.TryGetByBp(id : int) = x.TryGetByKey(id)
    member x.TryGetByBp(t : EveType) = x.TryGetByBp(t.Id)

    member x.GetByBp(id : int) = x.TryGetByBp(id).Value
    member x.GetByBp(t : EveType) = x.TryGetByBp(t).Value

    /// 行星生产
    member private x.InitPlanetSchematics() = 
        use archive = 
                let ResName = "BotData.EVEData.zip"
                let assembly = Reflection.Assembly.GetExecutingAssembly()
                let stream = assembly.GetManifestResourceStream(ResName)
                new Compression.ZipArchive(stream, IO.Compression.ZipArchiveMode.Read)
        use f = archive.GetEntry("planetschematicstypemap.json").Open()
        use r = new JsonTextReader(new StreamReader(f))

        // reactionTypeID * EveBlueprint
        let dict = Dictionary<int, EveBlueprint>()

        for item in JArray.Load(r) do 
            let item = item  :?> JObject
            let isInput = item.GetValue("isInput").ToObject<int>() = 1
            let q       = item.GetValue("quantity").ToObject<float>()
            // 转换成负数，以免和正常蓝图冲突
            let rid     = -(item.GetValue("schematicID").ToObject<int>())
            let tid     = item.GetValue("typeID").ToObject<int>()

            let em = {TypeId = tid; Quantity = q}
            let bp = 
                if dict.ContainsKey(rid) then
                    dict.[rid]
                else
                    {EveBlueprint.Default with Id = rid ; Type = BlueprintType.Planet}
            if isInput then
                dict.[rid] <- {bp with Materials = Array.append bp.Materials [|em|] }
            else
                dict.[rid] <- {bp with Products = Array.append bp.Products [|em|] }

        dict.Values

    /// 蓝图和反应
    member private x.InitBlueprints() = 
        seq {
            use archive = 
                    let ResName = "BotData.EVEData.zip"
                    let assembly = Reflection.Assembly.GetExecutingAssembly()
                    let stream = assembly.GetManifestResourceStream(ResName)
                    new Compression.ZipArchive(stream, IO.Compression.ZipArchiveMode.Read)
            use f = archive.GetEntry("blueprints.json").Open()
            use r = new JsonTextReader(new StreamReader(f))

            while r.Read() do 
                if r.TokenType = JsonToken.PropertyName then
                    r.Read() |> ignore
                    let o = JObject.Load(r)
                    let bpid = o.GetValue("blueprintTypeID").ToObject<int>()
                    let a = o.GetValue("activities") :?> JObject
                    if a.ContainsKey("manufacturing") then
                        let m = a.GetValue("manufacturing") :?> JObject
                        
                        let input = 
                            let hasInput = m.ContainsKey("materials")
                            if hasInput then
                                m.GetValue("materials").ToObject<EveMaterial []>()
                            else
                                Array.empty
                        let output = 
                            let hasOutput = m.ContainsKey("products")
                            if hasOutput then
                                m.GetValue("products").ToObject<EveMaterial []>()
                            else
                                Array.empty
                        yield {
                            Id = bpid
                            Materials = input
                            Products  = output
                            Type = BlueprintType.Manufacturing
                        }

                    // 以后再优化
                    elif a.ContainsKey("reaction") then
                        let m = a.GetValue("reaction") :?> JObject

                        let input = 
                            let hasInput = m.ContainsKey("materials")
                            if hasInput then
                                m.GetValue("materials").ToObject<EveMaterial []>()
                            else
                                Array.empty
                        let output = 
                            let hasOutput = m.ContainsKey("products")
                            if hasOutput then
                                m.GetValue("products").ToObject<EveMaterial []>()
                            else
                                Array.empty
                        yield {
                            Id = bpid
                            Materials = input
                            Products  = output
                            Type = BlueprintType.Reaction
                        }
        }