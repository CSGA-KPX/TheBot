namespace KPX.EvePlugin.Data.GameInternalPriceCache

open System

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open KPX.TheBot.Host
open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb

open KPX.EvePlugin.Data.EveType


[<CLIMutable>]
type GameInternalPrice =
    { [<LiteDB.BsonId(false)>]
      [<JsonProperty("type_id")>]
      Id: int
      [<JsonProperty("adjusted_price")>]
      AdjustedPrice: float
      [<JsonProperty("average_price")>]
      AveragePrice: float }

type GameInternalPriceCollection private () =
    inherit CachedTableCollection<GameInternalPrice>()

    static let instance = GameInternalPriceCollection()

    static member Instance = instance

    override x.IsExpired = (DateTimeOffset.Now - x.GetLastUpdateTime()) >= TimeSpan.FromDays(1.0)

    override x.Depends = [| typeof<EveTypeCollection> |]

    override x.InitializeCollection() =
        let url = "https://ali-esi.evepc.163.com/latest/markets/prices/?datasource=serenity"
        let resp = TheBotWebFetcher.fetch url
        use stream = resp.Content.ReadAsStream()
        use reader = new IO.StreamReader(stream)
        use jsonReader = new JsonTextReader(reader)

        JArray
            .Load(jsonReader)
            .ToObject<GameInternalPrice []>()
        |> x.DbCollection.InsertBulk
        |> ignore

    /// 返回物品内部价格，如果不存在，返回0.0
    member x.GetByItem(id: int) =
        x.CheckUpdate()
        let ret = x.DbCollection.TryFindById(id)

        if ret.IsNone then
            { Id = id
              AdjustedPrice = 0.0
              AveragePrice = 0.0 }
        else
            ret.Value

    member x.GetByItem(t: EveType) = x.GetByItem(t.Id)
