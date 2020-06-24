module EveData
open System
open System.Collections.Generic
open System.IO
open Newtonsoft.Json
open Newtonsoft.Json.Linq

let inline pct (i : int) = (float i) / 100.0

type EveGroup =
    {
        GroupId : int
        CategoryId : int
    }

    static member GetGroups() = 
        use archive = 
                let ResName = "BotData.EVEData.zip"
                let assembly = Reflection.Assembly.GetExecutingAssembly()
                let stream = assembly.GetManifestResourceStream(ResName)
                new IO.Compression.ZipArchive(stream, IO.Compression.ZipArchiveMode.Read)
        use f = archive.GetEntry("evegroups.json").Open()
        use r = new JsonTextReader(new StreamReader(f))

        JObject.Load(r).ToObject<Dictionary<int, EveGroup>>()

type EveType = 
    {
        TypeId : int
        TypeName : string
        GroupId : int
        CategoryId : int
        Volume : float
        MetaGroupId : int
    }

    static member GetEveTypes() = 
        seq {
            use archive = 
                    let ResName = "BotData.EVEData.zip"
                    let assembly = Reflection.Assembly.GetExecutingAssembly()
                    let stream = assembly.GetManifestResourceStream(ResName)
                    new IO.Compression.ZipArchive(stream, Compression.ZipArchiveMode.Read)
            use f = archive.GetEntry("evetypes.json").Open()
            use r = new JsonTextReader(new StreamReader(f))

            let gidMapping = EveGroup.GetGroups()

            let disallowCat = 
                [| 0; 1; 2; 11; 91;|]
                |> HashSet

            while r.Read() do 
                if r.TokenType = JsonToken.PropertyName then
                    r.Read() |> ignore
                    let o = JObject.Load(r)
                
                    let gid = o.GetValue("groupID").ToObject<int>()
                    let cat = gidMapping.[gid].CategoryId
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
                            0

                    let published = o.GetValue("published").ToObject<bool>()

                    if allow && published then
                        yield {
                            TypeId = tid
                            TypeName = name
                            GroupId = gid
                            CategoryId = cat
                            Volume = vol
                            MetaGroupId = meta
                        }
        }

type EveMaterial = 
    {
        Quantity : float
        TypeId   : int
    }

type BlueprintType = 
    | Unknown = 0
    | Manufacturing = 1
    | Planet = 2
    | Reaction = 3

type EveBlueprint = 
    {
        Materials : EveMaterial []
        Products : EveMaterial []
        BlueprintTypeID : int
        Type : BlueprintType
    }

    static member Default =
        {
            Materials = Array.empty
            Products = Array.empty
            BlueprintTypeID = 0
            Type = BlueprintType.Unknown
        }

    /// 计算所需流程数的材料，结果会ceil
    member x.GetBpByRuns(r : float) = 
        let ms = 
            x.Materials
            |> Array.map (fun m -> {m with Quantity = m.Quantity * r |> ceil})
        let ps = 
            x.Products
            |> Array.map (fun p -> {p with Quantity = p.Quantity * r |> ceil})

        {x with Materials = ms; Products = ps}

    /// 计算所需物品数量的材料，流程数向上取整
    /// 
    /// 用于材料计算
    member x.GetBpByItemCeil(q : float) = 
        let runs = q / x.ProductQuantity |> ceil
        x.GetBpByRuns(runs)

    /// 计算所需物品数量的材料
    ///
    /// 用于价格计算
    member x.GetBpByItemNoCeil(q : float) = 
        let runs = q / x.ProductQuantity
        let headRuns = runs |> floor
        let leftRuns = runs - headRuns

        let head = x.GetBpByRuns(headRuns)
        let one  = x.GetBpByRuns(1.0)

        let ms = 
            Array.map2 (fun h o ->
                if h.TypeId <> o.TypeId then failwith "GetBpByItemNoCeil :: TypeId mismatch"
                {h with Quantity = h.Quantity + o.Quantity * leftRuns}
            ) head.Materials one.Materials 

        let ps = 
            Array.map2 (fun h o ->
                if h.TypeId <> o.TypeId then failwith "GetBpByItemNoCeil :: TypeId mismatch"
                {h with Quantity = h.Quantity + o.Quantity * leftRuns}
            ) head.Products one.Products 
        
        {x with Materials = ms; Products = ps}

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

    static member GetBlueprints() = 

        let GetPlanetSchema() = 
            use archive = 
                    let ResName = "BotData.EVEData.zip"
                    let assembly = Reflection.Assembly.GetExecutingAssembly()
                    let stream = assembly.GetManifestResourceStream(ResName)
                    new IO.Compression.ZipArchive(stream, IO.Compression.ZipArchiveMode.Read)
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
                        {EveBlueprint.Default with BlueprintTypeID = rid ; Type = BlueprintType.Planet}
                if isInput then
                    dict.[rid] <- {bp with Materials = Array.append bp.Materials [|em|] }
                else
                    dict.[rid] <- {bp with Products = Array.append bp.Products [|em|] }

            dict.Values

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
                            Materials = input
                            Products  = output
                            BlueprintTypeID = bpid
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
                            Materials = input
                            Products  = output
                            BlueprintTypeID = bpid
                            Type = BlueprintType.Reaction
                        }

            // 回收相关资源，节约点内存
            (r :> IDisposable).Dispose()
            f.Dispose()
            archive.Dispose()
            yield! GetPlanetSchema()
        }

type RefineInfo = 
    {
        OreType : EveType
        Volume  : float
        RefineUnit : float
        Yields  : EveMaterial []
    }

    static member GetRefineInfos() = 
        seq {
            use archive = 
                    let ResName = "BotData.EVEData.zip"
                    let assembly = Reflection.Assembly.GetExecutingAssembly()
                    let stream = assembly.GetManifestResourceStream(ResName)
                    new IO.Compression.ZipArchive(stream, Compression.ZipArchiveMode.Read)
            use f = archive.GetEntry("typematerials.json").Open()
            use r = new JsonTextReader(new StreamReader(f))

            while r.Read() do 
                if r.TokenType = JsonToken.PropertyName then
                    let inputTypeId = r.Value :?> string |> int
                    r.Read() |> ignore
                    let o = JObject.Load(r)
                    let ms = o.GetValue("materials") :?> JArray
                    yield inputTypeId, [|
                        for m in ms do 
                            let m = m :?> JObject
                            let tid = m.GetValue("materialTypeID").ToObject<int>()
                            let q   = m.GetValue("quantity").ToObject<float>()
                            yield {TypeId = tid; Quantity = q}
                    |]
        }

type NpcCorporation =
    {
        CorporationID : int
        CorporationName : string
    }

    static member GetNpcCorporations() = 
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
                yield {CorporationID = cid; CorporationName = cname}
        }

type LoyaltyStoreOffer = 
    {
        OfferId : int
        IskCost : float
        LpCost  : float
        Offer   : EveMaterial
        Required: EveMaterial []
    }

[<Literal>]
/// 普通矿
let OreNames = "凡晶石,灼烧岩,干焦岩,斜长岩,奥贝尔石,水硼砂,杰斯贝矿,希莫非特,同位原矿,片麻岩,黑赭石,灰岩,克洛基石,双多特石,艾克诺岩,基腹断岩"

[<Literal>]
/// 冰矿
let IceNames = "白釉冰,冰晶矿,粗蓝冰,电冰体,富清冰,光滑聚合冰,黑闪冰,加里多斯冰矿,聚合冰体,蓝冰矿,朴白釉冰,清冰锥"

[<Literal>]
/// 卫星矿石
let MoonNames = "沸石,钾盐,沥青,柯石英,磷钇矿,独居石,铈铌钙钛矿,硅铍钇矿,菱镉矿,砷铂矿,钒铅矿,铬铁矿,钴酸盐,黑稀金矿,榍石,白钨矿,钒钾铀矿,锆石,铯榴石,朱砂"

[<Literal>]
/// 矿物
let MineralNames = "三钛合金,类晶体胶矿,类银超金属,同位聚合体,超新星诺克石,晶状石英核岩,超噬矿,莫尔石"