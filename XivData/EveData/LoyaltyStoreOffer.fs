namespace BotData.EveData.LoyaltyStoreOffer

open System
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open BotData.Common.Database
open BotData.Common.Network

open BotData.EveData.Utils
open BotData.EveData.NpcCorporation

type LoyaltyStoreOffer = 
    {
        OfferId : int
        IskCost : float
        LpCost  : float
        Offer   : EveMaterial
        Required: EveMaterial []
    }

type LpStore = 
    {
        [<LiteDB.BsonId(false)>]
        /// 军团Id
        Id : int
        Offers : LoyaltyStoreOffer []
    }

type LoyaltyStoreCollection private () = 
    inherit CachedItemCollection<int, LpStore>()

    static let instance = LoyaltyStoreCollection()

    static member Instance = instance

    /// LoyaltyStoreOffer暂不过期
    override x.IsExpired (_) = false

    override x.Depends = [| typeof<NpcCorporationoCollection> |]

    override x.FetchItem(corpId) = 
        let url =
            sprintf "https://esi.evepc.163.com/latest/loyalty/stores/%i/offers/?datasource=serenity" corpId
        x.Logger.Info(sprintf "Fetching %s" url)
        let json = hc
                    .GetStringAsync(url)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult()
        {
            Id = corpId
            Offers = 
                    [|  for item in JArray.Parse(json) do 
                            let item = item :?> JObject
                            // 无视所有分析点兑换
                            if not <| item.ContainsKey("ak_cost") then
                                let isk = item.GetValue("isk_cost").ToObject<float>()
                                let lp  = item.GetValue("lp_cost").ToObject<float>()
                                let id  = item.GetValue("offer_id").ToObject<int>()
                                let offers = 
                                    let q   = item.GetValue("quantity").ToObject<float>()
                                    let t   = item.GetValue("type_id").ToObject<int>()
                                    {EveMaterial.TypeId = t; Quantity = q}

                                let requires = 
                                    [| for ii in item.GetValue("required_items") :?> JArray do 
                                            let i = ii :?> JObject
                                            let iq = i.GetValue("quantity").ToObject<float>()
                                            let it = i.GetValue("type_id").ToObject<int>()
                                            yield {EveMaterial.TypeId = it; Quantity = iq} |]

                                yield { IskCost = isk
                                        LpCost  = lp
                                        OfferId = id
                                        Offer = offers
                                        Required = requires } |]
        }