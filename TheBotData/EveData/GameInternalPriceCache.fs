namespace KPX.TheBot.Data.EveData.GameInternalPriceCache

open System
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open KPX.TheBot.Data.Common.Database
open KPX.TheBot.Data.Common.Network

open KPX.TheBot.Data.EveData.Utils
open KPX.TheBot.Data.EveData.EveType

[<CLIMutable>]
type GameInternalPrice =
    { [<LiteDB.BsonId(false)>]
      [<JsonProperty("type_id")>]
      Id : int
      [<JsonProperty("adjusted_price")>]
      AdjustedPrice : float
      [<JsonProperty("average_price")>]
      AveragePrice : float }

type GameInternalPriceCollection private () =
    inherit CachedTableCollection<int, GameInternalPrice>()

    static let instance = GameInternalPriceCollection()

    static member Instance = instance

    override x.IsExpired =
        (DateTimeOffset.Now - x.GetLastUpdateTime())
        >= TimeSpan.FromDays(1.0)

    override x.Depends = [| typeof<EveTypeCollection> |]

    override x.InitializeCollection() =
        let url =
            "https://esi.evepc.163.com/latest/markets/prices/?datasource=serenity"

        x.Logger.Info(sprintf "Fetching %s" url)

        let json =
            hc
                .GetStringAsync(url)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()

        JArray
            .Parse(json)
            .ToObject<GameInternalPrice []>()
        |> x.DbCollection.InsertBulk
        |> ignore

    /// 返回物品内部价格，如果不存在，返回0.0
    member x.GetByItem(id : int) =
        x.CheckUpdate()
        let ret = x.TryGetByKey(id)

        if ret.IsNone then
            { Id = id
              AdjustedPrice = 0.0
              AveragePrice = 0.0 }
        else
            ret.Value

    member x.GetByItem(t : EveType) = x.GetByItem(t.Id)
