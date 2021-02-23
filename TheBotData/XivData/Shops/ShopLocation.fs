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
        use col = BotDataInitializer.XivCollectionChs

        let level =
            seq {
                for item in col.GetSheet("Level") do
                    let o = item.As<int>("Object")
                    let map = item.AsRow("Map")
                    let sf = map.As<uint16>("SizeFactor") |> int
                    let offsetX = map.As<int16>("Offset{X}") |> int
                    let offsetY = map.As<int16>("Offset{Y}") |> int

                    let x =
                        toMapCoordinate3d (sf, item.As<float>("X"), offsetX)

                    let y =
                        toMapCoordinate3d (sf, item.As<float>("Y"), offsetY)

                    if o <> 0 then
                        yield
                            o,
                            {| X = x
                               Y = y
                               Object = o
                               Territory = map.AsRow("PlaceName").As<string>("Name") |}
            }
            |> readOnlyDict

        let eNpcRes = col.GetSheet("ENpcResident")

        let data = Dictionary<int, ResizeArray<string>>()

        for item in col.GetSheet("ENpcBase") do
            let npc = item.Key.Main
            let mutable npcInfo = ""

            if level.ContainsKey(npc) then
                let npcPos = level.[npc]
                let npcName = eNpcRes.[npc].As<string>("Singular")
                npcInfo <- sprintf "%s: %s(%.1f, %.1f)" npcName npcPos.Territory npcPos.X npcPos.Y

            let propIds =
                item.AsArray<int>("ENpcData", 32)
                |> Array.filter ((<>) 0)

            for prop in propIds do
                if not <| data.ContainsKey(prop) then data.Add(prop, ResizeArray<string>())

                if npcInfo <> "" then data.[prop].Add(npcInfo)

        data
        |> Seq.map
            (fun kv ->
                { ShopPropId = kv.Key
                  Locations = kv.Value.ToArray() })
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.ShopPropIdExists(id : int) = 
        x.DbCollection.Exists(Query.EQ("_id", BsonValue(id)))

    member x.GetByShopPropId(id : int) = 
        x.DbCollection.SafeFindById(id)