namespace KPX.EvePlugin.Data.LoyaltyStoreOffer


open Newtonsoft.Json
open Newtonsoft.Json.Linq

open KPX.TheBot.Host
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataModel.Recipe

open KPX.EvePlugin.Data.Process
open KPX.EvePlugin.Data.NpcCorporation


[<CLIMutable>]
type LoyaltyStoreOffer =
    { OfferId: int
      IskCost: float
      LpCost: float
      Process: RecipeProcess<int> }

    member x.CastProcess() =
        let ec = KPX.EvePlugin.Data.EveType.EveTypeCollection.Instance

        { Materials =
            x.Process.Materials
            |> Array.map (fun mr ->
                { Item = ec.GetById(mr.Item)
                  Quantity = mr.Quantity })
          Product =
            { Item = ec.GetById(x.Process.Product.Item)
              Quantity = x.Process.Product.Quantity } }

[<CLIMutable>]
type LpStore =
    { [<LiteDB.BsonId(false)>]
      Id: int
      Offers: LoyaltyStoreOffer [] }

type LoyaltyStoreCollection private () =
    inherit CachedItemCollection<int, LpStore>()

    static let instance = LoyaltyStoreCollection()

    static member Instance = instance

    /// LoyaltyStoreOffer暂不过期
    override x.IsExpired _ = false

    override x.Depends = [| typeof<NpcCorporationCollection> |]

    override x.DoFetchItem(corpId) =
        let url = $"https://ali-esi.evepc.163.com/latest/loyalty/stores/%i{corpId}/offers/?datasource=serenity"
        let resp = TheBotWebFetcher.fetch url
        use stream = resp.Content.ReadAsStream()
        use reader = new System.IO.StreamReader(stream)
        use jsonReader = new JsonTextReader(reader)

        { Id = corpId
          Offers =
            [| for item in JArray.Load(jsonReader) do
                   let item = item :?> JObject
                   // 无视所有分析点兑换
                   let akCost = item.GetValue("ak_cost").ToObject<int>()

                   if akCost = 0 then
                       let isk = item.GetValue("isk_cost").ToObject<float>()

                       let lp = item.GetValue("lp_cost").ToObject<float>()

                       let id = item.GetValue("offer_id").ToObject<int>()

                       let offers =
                           let q = item.GetValue("quantity").ToObject<float>()

                           let t = item.GetValue("type_id").ToObject<int>()

                           { EveDbMaterial.Item = t
                             EveDbMaterial.Quantity = q }

                       let requires =
                           [| for ii in item.GetValue("required_items") :?> JArray do
                                  let i = ii :?> JObject
                                  let iq = i.GetValue("quantity").ToObject<float>()
                                  let it = i.GetValue("type_id").ToObject<int>()

                                  yield
                                      { EveDbMaterial.Item = it
                                        EveDbMaterial.Quantity = iq } |]

                       yield
                           { IskCost = isk
                             LpCost = lp
                             OfferId = id
                             Process =
                               { Materials = requires
                                 Product = offers } } |] }
