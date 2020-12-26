namespace KPX.TheBot.Data.EveData.SystemCostIndexCache

open System
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open KPX.TheBot.Data.Common.Database
open KPX.TheBot.Data.Common.Network

open KPX.TheBot.Data.EveData.Utils

[<CLIMutable>]
type SystemCostIndex =
    { Id : int
      Manufacturing : float
      ResearchTime : float
      ResearcMaterial : float
      Copying : float
      Invention : float
      Reaction : float }

    static member DefaultOf(systemId : int) =
        { Id = systemId
          Manufacturing = 0.0
          ResearchTime = 0.0
          ResearcMaterial = 0.0
          Copying = 0.0
          Invention = 0.0
          Reaction = 0.0 }

type SystemCostIndexCollection private () =
    inherit CachedTableCollection<int, SystemCostIndex>()

    static let instance = SystemCostIndexCollection()

    static member Instance = instance

    override x.IsExpired =
        (DateTimeOffset.Now - x.GetLastUpdateTime())
        >= TimeSpan.FromDays(1.0)

    override x.Depends =
        [| typeof<KPX.TheBot.Data.EveData.SolarSystems.SolarSystemCollection> |]

    override x.InitializeCollection() =
        let url =
            "https://esi.evepc.163.com/latest/industry/systems/?datasource=serenity"

        x.Logger.Info(sprintf "Fetching %s" url)

        let json =
            hc
                .GetStringAsync(url)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()

        seq {
            for item in JArray.Parse(json) do
                let item = item :?> JObject

                let sid =
                    item.GetValue("solar_system_id").ToObject<int>()

                let mutable ret = SystemCostIndex.DefaultOf(sid)
                let indices = item.GetValue("cost_indices") :?> JArray

                for indice in indices do
                    let indice = indice :?> JObject

                    let index =
                        indice.GetValue("cost_index").ToObject<float>()

                    match indice.GetValue("activity").ToObject<string>() with
                    | "manufacturing" -> ret <- { ret with Manufacturing = index }
                    | "researching_time_efficiency" -> ret <- { ret with ResearchTime = index }
                    | "researching_material_efficiency" ->
                        ret <- { ret with ResearcMaterial = index }
                    | "copying" -> ret <- { ret with Copying = index }
                    | "invention" -> ret <- { ret with Invention = index }
                    | "reaction" -> ret <- { ret with Reaction = index }
                    | unk -> failwithf "位置指数类型:%s" unk

                yield ret
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.TryGetBySystem(system : KPX.TheBot.Data.EveData.SolarSystems.SolarSystem) =
        x.CheckUpdate()
        x.TryGetByKey(system.Id)

    member x.GetBySystem(system : KPX.TheBot.Data.EveData.SolarSystems.SolarSystem) =
        x.PassOrRaise(x.TryGetByKey(system.Id), "没有{0}的工业指数资料", system.Name)
