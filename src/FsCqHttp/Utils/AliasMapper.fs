namespace KPX.FsCqHttp.Utils.AliasMapper

open System
open System.Collections.Generic

open KPX.FsCqHttp.Utils.TextResponse


/// 用于处理别名的集合
type AliasMapper() =
    let dict = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

    /// 都会小写转化。返回转化后的key
    member x.Add(key: string, [<ParamArray>] aliases: string []) =
        dict.Add(key, key)

        for alias in aliases do
            dict.Add(alias, key)

    member x.Map(value: string) =
        if dict.ContainsKey(value) then
            dict.[value]
        else
            raise <| KeyNotFoundException $"找不到名称%s{value}"

    member x.TryMap(value: string) =
        let succ, key = dict.TryGetValue(value)
        if succ then Some key else None

    member x.Contains(value: string) = dict.ContainsKey(value)

    member x.Keys = dict.Keys |> Seq.cast<string>

    member x.GetValueTable() =
        TextTable() {
            AsCols [ Literal("名称") { bold }
                     Literal("别名") { bold } ]

            dict
            |> Seq.groupBy (fun kv -> kv.Value)
            |> Seq.map (fun (key, aliases) ->
                let aliases =
                    aliases
                    |> Seq.filter (fun kv -> kv.Value <> kv.Key)
                    |> Seq.map (fun kv -> kv.Key)

                [ Literal(key); Literal(String.Join(", ", aliases)) ])
        }
