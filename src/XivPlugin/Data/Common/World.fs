namespace KPX.XivPlugin.Data

open System
open System.Collections.Generic

open KPX.TheBot.Host.Data


type World =
    { /// 服务器编号
      WorldId: int
      /// 服务器名称
      mutable WorldName: string
      /// 所属数据中心
      mutable DataCenter: string
      /// 是否开放
      mutable IsPublic: bool
      /// 所属发行区域
      mutable VersionRegion: VersionRegion }

module World =
    let private idMapping = Dictionary<int, World>()

    let private nameMapping = Dictionary<string, World>(StringComparer.OrdinalIgnoreCase)

    let private dcNameMapping = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

    /// <summary>
    /// 已经定义的服务器
    /// </summary>
    let AllWorlds = idMapping.Values |> Seq.readonly

    /// <summary>
    /// 所有公开服务器
    /// </summary>
    let PublicWorlds =
        seq {
            for v in idMapping.Values do
                if v.IsPublic then yield v
        }

    /// <summary>
    /// 所有服务器名称和别名
    /// </summary>
    let WorldNames =
        seq {
            for kv in nameMapping do
                if kv.Value.IsPublic then yield kv.Key
        }

    /// <summary>
    /// 所有数据中心名称
    /// </summary>
    let DataCenters = dcNameMapping.Keys |> Seq.readonly

    /// <summary>
    /// 检查是否存在指定名称的服务器
    /// </summary>
    /// <param name="name">服务器名称</param>
    let DefinedWorld (name: string) = nameMapping.ContainsKey(name)

    /// <summary>
    /// 检查是否存在指定名称的数据中心
    /// </summary>
    /// <param name="name">数据中心名称</param>
    let DefinedDC (name: string) = dcNameMapping.ContainsKey(name)

    let GetDCByName (name: string) = dcNameMapping.[name]

    let GetWorldById (id: int) = idMapping.[id]

    let GetWorldByName (name: string) = nameMapping.[name]

    let GetWorldsByDC (dcName: string) =
        let mapped = dcNameMapping.[dcName]

        PublicWorlds |> Seq.filter (fun w -> w.DataCenter = mapped)

    do
        // 添加已经定义的服务器
        let col = OfficalDistroData.GetCollection()

        // 世界服定义的DC
        for dc in col.WorldDCGroupType.TypedRows do
            let name = dc.Name.AsString()

            if dc.Region.AsInt() <> 0 then
                dcNameMapping.TryAdd(name, name) |> ignore

        // 世界服定义的服务器
        for world in col.World.TypedRows do
            let id = world.Key.Main
            let name = world.Name.AsString()
            let isPublic = world.IsPublic.AsBool()
            let dc = world.DataCenter.AsRow().Name.AsString()

            let world =
                { WorldId = id
                  WorldName = name
                  DataCenter = dc
                  IsPublic = isPublic
                  VersionRegion = VersionRegion.Offical }

            idMapping.Add(id, world)

            if not <| nameMapping.TryAdd(name, world) then
                printfn $"World : 服务器添加失败 %A{world}"

        // 处理自定义数据
        let res = ResxManager("XivPlugin.XivStrings")

        // 处理国服的大区信息
        for lines in res.GetLines("ChsDCInfo") do
            let data = lines.Split(' ')
            let dcName = data.[0]
            let worlds = data.[1].Split(',')

            for world in worlds do
                let mutable world = GetWorldByName(world)
                world.VersionRegion <- VersionRegion.China
                world.DataCenter <- dcName
                world.IsPublic <- true

        // 处理大区别名
        for lines in res.GetLines("DCNameAlias") do
            let data = lines.Split(' ')
            let dc = data.[0]
            let aliases = data.[1].Split(',')
            dcNameMapping.TryAdd(dc, dc) |> ignore

            for alias in aliases do
                dcNameMapping.TryAdd(alias, dc) |> ignore

        // 处理服务器别名
        for lines in res.GetLinesWithoutComment("WorldNameAlias", "//") do
            let data = lines.Split(' ')
            let world = data.[0]

            let aliases =
                data.[1]
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)

            for alias in aliases do
                nameMapping.TryAdd(alias, GetWorldByName(world)) |> ignore
