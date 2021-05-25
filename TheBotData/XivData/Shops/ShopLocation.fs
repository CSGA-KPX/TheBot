namespace KPX.TheBot.Data.XivData.Shops

open System.Collections.Generic

open LiteDB

open KPX.TheBot.Data.Common.Database


type ShopLocation =
    { [<BsonId(false)>]
      ShopPropId : int
      Locations : string [] }


/// 提供ShopPropId（即部分ENpcData）到商店位置的缓存
type ShopLocationCollection private () =
    inherit CachedTableCollection<int, ShopLocation>(DefaultDB)

    static let instance = ShopLocationCollection()

    let toMapCoordinate3d (sizeFactor : int, value : float, offset : int) =
        let c = (float sizeFactor) / 100.0
        let offsetValue = (value + (float offset)) * c

        (41.0 / c) * ((offsetValue + 1024.0) / 2048.0)
        + 1.0

    static member Instance = instance

    override x.IsExpired = false

    override x.Depends = Array.empty

    override x.InitializeCollection() =
        let col = BotDataInitializer.XivCollectionChs

        let level =
            seq {
                for row in col.Level.TypedRows do
                    let map = row.Map.AsRow()
                    let sf = map.SizeFactor.AsInt()

                    let x =
                        let x = row.X.AsDouble()
                        let offsetX = map.``Offset{X}``.AsInt()
                        toMapCoordinate3d (sf, x, offsetX)

                    let y =
                        let y = row.Y.AsDouble()
                        let offsetY = map.``Offset{Y}``.AsInt()
                        toMapCoordinate3d (sf, y, offsetY)

                    let o = row.Object.AsInt()

                    if o <> 0 then
                        yield
                            o,
                            {| X = x
                               Y = y
                               Object = o
                               Territory = map.PlaceName.AsRow().Name.AsString() |}
            }
            |> readOnlyDict

        let data = Dictionary<int, ResizeArray<string>>()

        let eNpcRes = col.ENpcResident

        for row in col.ENpcBase.TypedRows do
            let npcId = row.Key.Main
            let mutable npcInfo = ""

            if level.ContainsKey(npcId) then
                let npcPos = level.[npcId]

                let npcName =
                    eNpcRes.GetItemTyped(npcId).Singular.AsString()

                npcInfo <- $"%s{npcName}: %s{npcPos.Territory}(%.1f{npcPos.X}, %.1f{npcPos.Y})"

            for propId in row.ENpcData.AsInts() do
                if propId <> 0 then
                    if not <| data.ContainsKey(propId) then
                        data.Add(propId, ResizeArray<string>())

                    if npcInfo <> "" then data.[propId].Add(npcInfo)

        data
        |> Seq.map
            (fun kv ->
                { ShopPropId = kv.Key
                  Locations = kv.Value.ToArray() })
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.ShopPropIdExists(id : int) =
        x.DbCollection.Exists(Query.EQ("_id", BsonValue(id)))

    member x.GetByShopPropId(id : int) = x.DbCollection.SafeFindById(id)
