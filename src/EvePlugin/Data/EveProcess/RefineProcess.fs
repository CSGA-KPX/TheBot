namespace KPX.EvePlugin.Data.Process

open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache.LiteDb
open KPX.TheBot.Host.DataModel.Recipe

open KPX.EvePlugin.Data.EveType


/// 精炼
type RefineProcessCollection private () =
    inherit EveProcessCollection()

    static let instance = RefineProcessCollection()

    static member Instance = instance

    override x.InitializeCollection() =
        seq {
            use archive = KPX.EvePlugin.Data.Utils.GetEveDataArchive()

            use f = archive.GetEntry("typematerials.json").Open()

            use r = new JsonTextReader(new StreamReader(f))

            while r.Read() do
                if r.TokenType = JsonToken.PropertyName then
                    let inputTypeId = r.Value :?> string |> int
                    r.Read() |> ignore
                    let o = JObject.Load(r)
                    let ms = o.GetValue("materials") :?> JArray

                    let yields =
                        [| for m in ms do
                               let m = m :?> JObject

                               let tid = m.GetValue("materialTypeID").ToObject<int>()

                               let q = m.GetValue("quantity").ToObject<float>()
                               yield { Item = tid; Quantity = q } |]

                    let t = EveTypeCollection.Instance.TryGetById(inputTypeId)

                    if t.IsSome then
                        let t = t.Value

                        yield
                            { Id = inputTypeId
                              Process =
                                  { Input =
                                        Array.singleton
                                            { Item = t.Id
                                              Quantity = t.PortionSize |> float }
                                    Output = yields }
                              Type = ProcessType.Refine }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.GetProcessFor(item: EveType) =
        x
            .DbCollection
            .SafeFindById(item.Id)
            .AsEveProcess()
