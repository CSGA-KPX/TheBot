namespace KPX.FsCqHttp.Utils.UserOption

open System
open System.Collections.Generic

open KPX.FsCqHttp.Utils.AliasMapper


[<DefaultAugmentation(false)>]
type UserOptionValue =
    | Defined of string list
    | Default of string

    member x.SetOrAppend(value : string) =
        let isEmpty = String.IsNullOrWhiteSpace(value)

        match x, isEmpty with
        | Defined l, true -> Defined(x.Value :: l) //空的话加现有值
        | Defined l, false -> Defined(value :: l)
        | Default v, true -> Defined [ v ]
        | Default _, false -> Defined [ value ]

    member x.IsDefined =
        match x with
        | Defined _ -> true
        | _ -> false

    member x.Value =
        match x with
        | Defined x -> x.[0]
        | Default x -> x

    member x.Values =
        match x with
        | Defined x -> x |> List.toArray |> Array.rev
        | Default x -> [| x |]

    member x.GetValue<'T when 'T :> IConvertible>() =
        Convert.ChangeType(x.Value, typeof<'T>) :?> 'T

    member x.GetValues<'T when 'T :> IConvertible>() =
        [| for v in x.Values do
            yield Convert.ChangeType(v, typeof<'T>) :?> 'T |]

type UserOptionParser() =
    static let seperator = [| ";"; "；"; "："; ":" |]
    let mutable parsed = false
    let mutable cmdLine = Array.empty<string>
    let options = Dictionary<string, UserOptionValue>()

    let values = List<string * UserOptionValue>()
    let mapper = AliasMapper()

    member x.RegisterOption(name : string, value : string, ?aliases : string []) =
        let name =
            mapper.Add(name, defaultArg aliases Array.empty)

        values.Add(name, Default value)

    member x.GetValue<'T when 'T :> IConvertible>(key : string) =
        if not parsed then invalidOp "还没解析过"
        let key = mapper.Map(key)
        options.[key].GetValue<'T>()

    member x.GetValue(key : string) = x.GetValue<string>(key)

    member x.GetValues<'T when 'T :> IConvertible>(key : string) =
        if not parsed then invalidOp "还没解析过"
        let key = mapper.Map(key)
        options.[key].GetValues<'T>()

    member x.GetValues(key : string) = x.GetValues<string>(key)

    member x.IsDefined(key : string) =
        if not parsed then invalidOp "还没解析过"
        let key = mapper.Map(key)
        options.[key].IsDefined

    member x.Parse(input : string []) =
        if parsed then invalidOp "已经解析过了"

        for (k, v) in values do
            // 复制默认选项到options
            options.Add(k, v)

        cmdLine <-
            [| for str in input do
                let contains =
                    seperator
                    |> Array.exists (fun s -> str.Contains(s))

                if contains then
                    let s =
                        str.Split(seperator, 2, StringSplitOptions.RemoveEmptyEntries)

                    let key = mapper.Map(s.[0])
                    let value = if s.Length >= 2 then s.[1] else ""

                    if options.ContainsKey(key) then options.[key] <- options.[key].SetOrAppend(value) else yield str
                else
                    yield str |]

        parsed <- true

    /// 解析后剩余的文本
    member x.CommandLine = cmdLine

    /// 解析后剩余的文本拼接为字符串
    member x.CmdLineAsString = String.Join(" ", cmdLine)
