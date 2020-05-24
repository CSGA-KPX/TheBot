module EveData
open System
open System.Collections.Generic
open System.Text
open System.IO
open Newtonsoft.Json
open Newtonsoft.Json.Linq

let inline pct (i : int) = (float i) / 100.0

type EveType = 
    {
        TypeId : int
        TypeName : string
    }

type EveMaterial = 
    {
        Quantity : float
        TypeId   : int
    }

type EveBlueprint = 
    {
        Materials : EveMaterial []
        Products : EveMaterial []
        BlueprintTypeID : int
    }

    /// 计算所需流程数的材料，结果会ceil
    member x.GetBlueprintByRuns(r : float) = 
        let ms = 
            x.Materials
            |> Array.map (fun m -> {m with Quantity = m.Quantity * r |> ceil})
        let ps = 
            x.Products
            |> Array.map (fun p -> {p with Quantity = p.Quantity * r |> ceil})

        {x with Materials = ms; Products = ps}

    /// 计算所需物品数量的材料，结果会ceil
    member x.GetBlueprintByItems(q : float) = 
        let runs = q / x.ProductQuantity
        x.GetBlueprintByRuns(runs)

    /// 根据材料效率调整材料数量
    member x.ApplyMaterialEfficiency(me : int) =
        let factor = (100 - me) |> pct
        let ms = 
            x.Materials
            |> Array.map (fun m -> 
                let q = (float m.Quantity) * factor
                {m with Quantity = q}
            )
        {x with Materials = ms}

    /// 仅有一个产品时返回材料Id，其他则抛出异常
    member x.ProductId = (x.Products |> Array.head).TypeId

    /// 仅有一个产品时返回材料数量，其他则抛出异常
    member x.ProductQuantity = (x.Products |> Array.head).Quantity

type EveGroup =
    {
        GroupId : int
        CategoryId : int
    }

let GetGroup() =
    use archive = 
            let ResName = "BotData.EVEData.zip"
            let assembly = Reflection.Assembly.GetExecutingAssembly()
            let stream = assembly.GetManifestResourceStream(ResName)
            new IO.Compression.ZipArchive(stream, IO.Compression.ZipArchiveMode.Read)
    use f = archive.GetEntry("evegroups.json").Open()
    use r = new JsonTextReader(new StreamReader(f))

    JObject.Load(r).ToObject<Dictionary<int, EveGroup>>()
    

let GetEveTypes() = 
    seq {
        use archive = 
                let ResName = "BotData.EVEData.zip"
                let assembly = Reflection.Assembly.GetExecutingAssembly()
                let stream = assembly.GetManifestResourceStream(ResName)
                new IO.Compression.ZipArchive(stream, IO.Compression.ZipArchiveMode.Read)
        use f = archive.GetEntry("evetypes.json").Open()
        use r = new JsonTextReader(new StreamReader(f))

        let gidMapping = GetGroup()

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

                if tid = 37170 then name <- "屹立大型设备制造效率 I"
                
                let published = o.GetValue("published").ToObject<bool>()

                if allow && published then
                    yield {
                        TypeId = tid
                        TypeName = name
                    }
    }

let GetBlueprints() = 
    seq {
        use archive = 
                let ResName = "BotData.EVEData.zip"
                let assembly = Reflection.Assembly.GetExecutingAssembly()
                let stream = assembly.GetManifestResourceStream(ResName)
                new IO.Compression.ZipArchive(stream, IO.Compression.ZipArchiveMode.Read)
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
                    }
    }
