namespace BotData.EveData.RefineInfo

open System
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open BotData.Common.Database

open BotData.EveData.Utils
open BotData.EveData.EveType

[<CLIMutable>]
type RefineInfo = 
    {
        [<LiteDB.BsonId(false)>]
        Id  : int
        InputType : EveType
        RefineUnit : float
        Yields  : EveMaterial []
    }

type RefineInfoCollection private () = 
    inherit CachedTableCollection<int, RefineInfo>()

    static let instance = RefineInfoCollection()

    static member Instance = instance

    override x.IsExpired = false

    override x.Depends = [| typeof<EveTypeCollection> |]

    override x.InitializeCollection() =
        seq {
            use archive = 
                    let ResName = "BotData.EVEData.zip"
                    let assembly = Reflection.Assembly.GetExecutingAssembly()
                    let stream = assembly.GetManifestResourceStream(ResName)
                    new Compression.ZipArchive(stream, Compression.ZipArchiveMode.Read)
            use f = archive.GetEntry("typematerials.json").Open()
            use r = new JsonTextReader(new StreamReader(f))

            while r.Read() do 
                if r.TokenType = JsonToken.PropertyName then
                    let inputTypeId = r.Value :?> string |> int
                    r.Read() |> ignore
                    let o = JObject.Load(r)
                    let ms = o.GetValue("materials") :?> JArray
                    let yields = [|
                        for m in ms do 
                            let m = m :?> JObject
                            let tid = m.GetValue("materialTypeID").ToObject<int>()
                            let q   = m.GetValue("quantity").ToObject<float>()
                            yield {TypeId = tid; Quantity = q}
                    |]
                    let t = EveTypeCollection.Instance.TryGetById(inputTypeId)
                    if t.IsSome then
                        let t = t.Value
                        yield {
                            Id = inputTypeId
                            InputType = t
                            RefineUnit = t.PortionSize |> float
                            Yields = yields }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.GetByItem(id : int) = x.GetByKey(id)

    member x.GetByItem(i : EveType) = x.GetByKey(i.Id)