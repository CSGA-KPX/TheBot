(*

转储所有藏宝图位置信息

*)

#I "../build/bin/"
#I "../build/bin/plugins/XivPlugin/"

#r "McMaster.NETCore.Plugins.dll"
#r "FsCqHttp.dll"
#r "TheBot.dll"
#r "Nlog.dll"
#r "LiteDB.dll"
#r @"XivPlugin.dll"
#r @"LibFFXIV.GameData.dll"
#r @"CsvParser.dll"

#r "nuget: EPPlus, 6.0.2-beta"


open System
open System.Drawing
open System.IO
open System.Collections.Generic
open System.Reflection

open KPX.TheBot.Host

open KPX.XivPlugin.Data


Environment.CurrentDirectory <- Path.Join(__SOURCE_DIRECTORY__, "../build/bin/")
let discover = HostedModuleDiscover()
discover.ScanAssembly(Assembly.GetAssembly(typeof<ItemCollection>))
discover.AddModule(KPX.TheBot.Module.DataCacheModule.DataCacheModule(discover))

// 以上用于加载必须的配置文件，请勿改动

let col = ChinaDistroData.GetCollection()

let treasureSpots =
    col.TreasureSpot
    |> Seq.filter (fun x -> x.Location.AsInt() <> 0)
    |> Seq.toArray

let treasureRanks =
    col.TreasureHuntRank
    |> Seq.filter (fun r -> r.Icon.AsInt() <> 0)
    // ItemName是一般道具，KeyItemName是任务道具
    |> Seq.map (fun r -> r.Key.Main, r.ItemName.AsRow().Name.AsString())
    |> readOnlyDict

/// 将表格坐标转换到游戏内坐标
let ToMapCoordinate3d (sizeFactor: int, value: single, offset: int) =
    let c = (float sizeFactor) / 100.0
    let offsetValue = ((float value) + (float offset)) * c

    (41.0 / c) * ((offsetValue + 1024.0) / 2048.0)
    + 1.0


let treasureLocations =
    for row in
        col.TreasureSpot
        |> Seq.filter (fun x -> x.Location.AsInt() <> 0) do

        let loc = row.Location.AsRow()
        let map = loc.Map.AsRow()

        let mapSizeFactor = map.SizeFactor.AsInt()
        let mapOffsetX = map.``Offset{X}``.AsInt()
        let mapOffsetY = map.``Offset{Y}``.AsInt()
        let mapName = map.PlaceName.AsRow().Name.AsString()

        let mapGameX = ToMapCoordinate3d(mapSizeFactor, loc.X.AsSingle(), mapOffsetX)
        let mapGameY = ToMapCoordinate3d(mapSizeFactor, loc.Y.AsSingle(), mapOffsetY)
        let rankName = treasureRanks.[row.Key.Main]

        printfn $"{rankName}:{row.Key.Alt} {mapName} (%.1f{mapGameX}, %.1f{mapGameY})"
