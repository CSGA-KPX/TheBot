namespace KPX.XivPlugin.Data.Shop

open System.Collections.Generic

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb

open KPX.XivPlugin
open KPX.XivPlugin.Data

open LiteDB


[<CLIMutable>]
type ShopLocation =
    { [<BsonId>]
      LiteDbId: int
      ShopPropId: int
      Region: VersionRegion
      Locations: string [] }

module private Utils =
    /// <summary>
    /// 将游戏设定坐标转换为地图坐标
    /// </summary>
    /// <param name="sizeFactor">尺寸系数</param>
    /// <param name="value">被转换的数值</param>
    /// <param name="offset">偏移量</param>
    let inline toMapCoordinate3d (sizeFactor: int, value: float, offset: int) =
        let c = (float sizeFactor) / 100.0
        let offsetValue = (value + (float offset)) * c

        (41.0 / c) * ((offsetValue + 1024.0) / 2048.0) + 1.0

/// <summary>
/// 提供ShopPropId（即部分ENpcData）到商店位置的缓存
/// </summary>
type ShopLocationCollection private () =
    inherit CachedTableCollection<ShopLocation>()

    static member val Instance = ShopLocationCollection()

    override x.IsExpired = false

    override x.Depends = Array.empty

    override x.InitializeCollection() =
        x.DbCollection.EnsureIndex(fun x -> x.ShopPropId) |> ignore
        x.InitChs()
        x.InitOffical()

    member private x.InitChs() =
        let col = ChinaDistroData.GetCollection()

        let level =
            seq {
                for row in col.Level do
                    let map = row.Map.AsRow()
                    let sf = map.SizeFactor.AsInt()

                    let x =
                        let x = row.X.AsDouble()
                        let offsetX = map.``Offset{X}``.AsInt()
                        Utils.toMapCoordinate3d (sf, x, offsetX)

                    let y =
                        let y = row.Y.AsDouble()
                        let offsetY = map.``Offset{Y}``.AsInt()
                        Utils.toMapCoordinate3d (sf, y, offsetY)

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

        for row in col.ENpcBase do
            let npcId = row.Key.Main
            let mutable npcInfo = ""

            if level.ContainsKey(npcId) then
                let npcPos = level.[npcId]
                let npcName = eNpcRes.[npcId].Singular.AsString()
                npcInfo <- $"%s{npcName}: %s{npcPos.Territory}(%.1f{npcPos.X}, %.1f{npcPos.Y})"

            for propId in row.ENpcData.AsInts() do
                if propId <> 0 then
                    if not <| data.ContainsKey(propId) then
                        data.Add(propId, ResizeArray<string>())

                    if npcInfo <> "" then
                        data.[propId].Add(npcInfo)

        data
        |> Seq.map (fun kv ->
            { LiteDbId = 0
              Region = VersionRegion.China
              ShopPropId = kv.Key
              Locations = kv.Value.ToArray() })
        |> x.DbCollection.InsertBulk
        |> ignore

    member private x.InitOffical() =
        let col = OfficalDistroData.GetCollection()

        let level =
            seq {
                for row in col.Level do
                    let map = row.Map.AsRow()
                    let sf = map.SizeFactor.AsInt()

                    let x =
                        let x = row.X.AsDouble()
                        let offsetX = map.``Offset{X}``.AsInt()
                        Utils.toMapCoordinate3d (sf, x, offsetX)

                    let y =
                        let y = row.Y.AsDouble()
                        let offsetY = map.``Offset{Y}``.AsInt()
                        Utils.toMapCoordinate3d (sf, y, offsetY)

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

        for row in col.ENpcBase do
            let npcId = row.Key.Main
            let mutable npcInfo = ""

            if level.ContainsKey(npcId) then
                let npcPos = level.[npcId]

                let npcName = eNpcRes.[npcId].Singular.AsString()

                npcInfo <- $"%s{npcName}: %s{npcPos.Territory}(%.1f{npcPos.X}, %.1f{npcPos.Y})"

            for propId in row.ENpcData.AsInts() do
                if propId <> 0 then
                    if not <| data.ContainsKey(propId) then
                        data.Add(propId, ResizeArray<string>())

                    if npcInfo <> "" then
                        data.[propId].Add(npcInfo)

        data
        |> Seq.map (fun kv ->
            { LiteDbId = 0
              Region = VersionRegion.Offical
              ShopPropId = kv.Key
              Locations = kv.Value.ToArray() })
        |> x.DbCollection.InsertBulk
        |> ignore

    /// <summary>
    /// 检查指定区域内是否存在商店Id
    /// </summary>
    /// <param name="id"></param>
    /// <param name="region"></param>
    member x.ShopPropIdExists(id: int, region: VersionRegion) =
        Query.And(Query.EQ("ShopPropId", id), Query.EQ("Region", region.BsonValue))
        |> x.DbCollection.Exists

    /// <summary>
    /// 获取指定区域和id的信息
    /// </summary>
    /// <param name="id"></param>
    /// <param name="region"></param>
    member x.GetByShopPropId(id: int, region: VersionRegion) =
        Query.And(Query.EQ("ShopPropId", id), Query.EQ("Region", region.BsonValue))
        |> x.DbCollection.SafeFindOne

    /// <summary>
    /// 尝试获取指定区域和id的信息
    /// </summary>
    /// <param name="id"></param>
    /// <param name="region"></param>
    member x.TryGetByShopPropId(id: int, region: VersionRegion) =
        Query.And(Query.EQ("ShopPropId", id), Query.EQ("Region", region.BsonValue))
        |> x.DbCollection.TryFindOne
