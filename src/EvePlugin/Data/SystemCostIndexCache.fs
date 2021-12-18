namespace KPX.EvePlugin.Data.SystemCostIndexCache

open System

open Newtonsoft.Json.Linq

open KPX.TheBot.Host
open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb


[<CLIMutable>]
type SystemCostIndex =
    { Id: int
      Manufacturing: float
      ResearchTime: float
      ResearchMaterial: float
      Copying: float
      Invention: float
      Reaction: float }

    static member DefaultOf(systemId: int) =
        { Id = systemId
          Manufacturing = 0.0
          ResearchTime = 0.0
          ResearchMaterial = 0.0
          Copying = 0.0
          Invention = 0.0
          Reaction = 0.0 }

type SystemCostIndexCollection private () =
    inherit CachedTableCollection<int, SystemCostIndex>()

    static let instance = SystemCostIndexCollection()

    static member Instance = instance

    override x.IsExpired = (DateTimeOffset.Now - x.GetLastUpdateTime()) >= TimeSpan.FromDays(1.0)

    override x.Depends = [| typeof<KPX.EvePlugin.Data.SolarSystems.SolarSystemCollection> |]

    override x.InitializeCollection() =
        let url = "https://esi.evepc.163.com/latest/industry/systems/?datasource=serenity"

        x.Logger.Info $"Fetching %s{url}"

        let json =
            Network
                .HttpClient
                .GetStringAsync(url)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()

        seq {
            for item in JArray.Parse(json) do
                let item = item :?> JObject

                let sid = item.GetValue("solar_system_id").ToObject<int>()

                let mutable ret = SystemCostIndex.DefaultOf(sid)
                let indices = item.GetValue("cost_indices") :?> JArray

                for idx in indices do
                    let idx = idx :?> JObject

                    let index = idx.GetValue("cost_index").ToObject<float>()

                    match idx.GetValue("activity").ToObject<string>() with
                    | "manufacturing" -> ret <- { ret with Manufacturing = index }
                    | "researching_time_efficiency" -> ret <- { ret with ResearchTime = index }
                    | "researching_material_efficiency" -> ret <- { ret with ResearchMaterial = index }
                    | "copying" -> ret <- { ret with Copying = index }
                    | "invention" -> ret <- { ret with Invention = index }
                    | "reaction" -> ret <- { ret with Reaction = index }
                    | unk -> failwithf $"位置指数类型:%s{unk}"

                yield ret
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.TryGetBySystem(system: KPX.EvePlugin.Data.SolarSystems.SolarSystem) =
        x.CheckUpdate()
        x.DbCollection.TryFindById(system.Id)

    member x.GetBySystem(system: KPX.EvePlugin.Data.SolarSystems.SolarSystem) =
        x.PassOrRaise(x.DbCollection.TryFindById(system.Id), "没有{0}的工业指数资料", system.Name)
