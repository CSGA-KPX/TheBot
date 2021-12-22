namespace KPX.XivPlugin.DataModel

open System
open System.Collections.Generic

open KPX.TheBot.Host.Data


[<Struct>]
type ClassJob =
    | ClassJob of string
    
    member x.Value =
        let (ClassJob v) = x
        v

type ClassJobMapping = { Key: string; Value: ClassJob }

module ClassJobMapping =
    open KPX.XivPlugin


    let private mapping = Dictionary<string, ClassJobMapping>()

    /// <summary>
    /// 根据名称查找职业
    /// </summary>
    /// <param name="str">名称</param>
    let Parse (str) = mapping.[str].Value

    /// <summary>
    /// 根据名称尝试查找职业
    /// </summary>
    /// <param name="str">名称</param>
    let TryParse (str) =
        let succ, item = mapping.TryGetValue(str)
        if succ then Some item.Value else None

    do
        seq {
            // 来自国服定义
            let col = ChinaDistroData.GetCollection()

            for row in col.ClassJob.TypedRows do
                let abbr = row.Abbreviation.AsString()
                yield abbr, abbr
                yield row.Name.AsString(), abbr
                yield row.RAW_2.AsString(), abbr

            // 来自资源文件的补充项目
            let res = ResxManager("XivPlugin.Resources.XivStrings")

            for lines in res.GetLines("ClassJobMapping") do
                let data = lines.Split(' ')
                yield data.[0], data.[1]

            // 以后挪到资源文件里
            // 还有6.0的几个职业
            yield! [| "占星", "AST"; "诗人", "BRD" |]
        }
        |> Seq.filter (fun (a, _) -> not <| String.IsNullOrWhiteSpace(a))
        |> Seq.iter
            (fun (a, b) ->
                let item = { Key = a; Value = ClassJob b }
                mapping.Add(a, item))
