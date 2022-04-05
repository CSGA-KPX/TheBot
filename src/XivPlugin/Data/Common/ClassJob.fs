namespace KPX.XivPlugin.Data

open System
open System.Collections.Generic

open KPX.TheBot.Host.Data

open LiteDB


/// 插件内部所用得三字母职业代码
[<Struct>]
type ClassJob internal (str: string) =

    static let mapping = Dictionary<string, ClassJobMapping>()

    static do
        seq {
            // 来自国服定义
            let col = ChinaDistroData.GetCollection()

            for row in col.ClassJob do
                let abbr = row.Abbreviation.AsString()
                yield abbr, abbr
                yield row.Name.AsString(), abbr
                yield row.RAW_2.AsString(), abbr

            let col = OfficalDistroData.GetCollection()

            for row in col.ClassJob do
                let abbr = row.Abbreviation.AsString()
                yield abbr, abbr
                yield row.Name.AsString(), abbr
                yield row.RAW_2.AsString(), abbr

            // 来自资源文件的补充项目
            let res = ResxManager("XivPlugin.XivStrings")

            for lines in res.GetLines("ClassJobMapping") do
                let data = lines.Split(' ')
                yield data.[0], data.[1]

            // 以后挪到资源文件里
            // 还有6.0的几个职业
            yield! [| "占星", "AST"; "诗人", "BRD" |]
        }
        |> Seq.filter (fun (a, _) -> not <| String.IsNullOrWhiteSpace(a))
        |> Seq.iter (fun (a, b) ->
            let item = { Name = a; Code = ClassJob(b) }
            mapping.TryAdd(a, item) |> ignore)

    override x.ToString() = str

    static member Parse(str: string) = mapping.[str].Code

    static member TryParse(str: string) =
        let succ, item = mapping.TryGetValue(str)
        if succ then Some item.Code else None

    static member internal GetMapping(str: string) = mapping.[str]

and ClassJobMapping = { Name: string; Code: ClassJob }

module ClassJob =
    let BattleClassJobs, GatherJobs, CraftJobs, CraftGatherJobs =
        let col = OfficalDistroData.GetCollection()

        let nameMapping = Dictionary<string, string>()

        for row in col.ClassJob do
            let key = row.Abbreviation.AsString()
            let name = row.``Name{English}``.AsString()
            nameMapping.[key] <- name

        // 用已知的中文名称替换
        for row in ChinaDistroData.GetCollection().ClassJob do
            let key = row.Abbreviation.AsString()
            let name = row.Name.AsString()

            if not <| String.IsNullOrWhiteSpace(name) then
                nameMapping.[key] <- name

        let header = col.ClassJobCategory.Header.Headers
        // 32 大地使者
        // 33 能工巧匠
        // 35 能工巧匠 大地使者
        // 110 战斗精英 魔法导师 特职专用

        let readData (headerId: int) =
            Seq.zip header (col.ClassJobCategory.[headerId].RawData)
            |> Seq.choose (fun (hdr, value) ->
                if value = "True" then
                    Some(ClassJob.GetMapping(nameMapping.[hdr.ColumnName]))
                else
                    None)
            |> Seq.cache

        let battle = readData 110
        let gather = readData 32
        let craft = readData 33
        let craftGather = readData 35

        (battle, gather, craft, craftGather)

type ClassJobCodeInstructions() =
    inherit KPX.TheBot.Host.PluginPrerunInstruction()

    override x.RunInstructions() =
        BsonMapper.Global.RegisterType<ClassJob>(
            (fun x -> BsonValue(x.ToString())),
            (fun v -> ClassJob(v.AsString))
        )
