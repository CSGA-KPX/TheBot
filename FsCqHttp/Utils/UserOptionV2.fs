namespace rec KPX.FsCqHttp.Utils.UserOptionV2

open System
open System.Collections.Generic

open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Utils.TextResponse


type OptionCell(cb : OptionBase, key : string) =
    /// 该选项的主要键名
    member x.KeyName = key

    /// 指示该选项是否已被设置
    member x.IsDefined = x.TryGetRealKey().IsSome

    /// 获取或更改该选项的别名
    member val Aliases = Array.empty<string> with get, set

    /// 查找第一个有效的参数名
    member internal x.TryGetRealKey() : string option =
        if cb.IsDefined(key) then
            Some key
        else
            x.Aliases
            |> Array.tryFind (fun alias -> cb.IsDefined(alias))

[<AbstractClass>]
type OptionCell<'T>(cb : OptionBase, key : string, defValue : 'T) =
    inherit OptionCell(cb, key)

    /// 获取或更改该选项的默认值
    member val DefaultValue = defValue with get, set
    
    abstract ConvertValue : string -> 'T
    
    /// 获取第一个设定值，如果没有则返回默认值
    member x.DefaultOrHead() =
        x.TryGetRealKey()
        |> Option.map
            (fun kn ->
                let ret = cb.GetDefined(kn)

                if ret.Count = 0 then
                    x.DefaultValue
                else
                    x.ConvertValue(ret.[0]))
        |> Option.defaultValue x.DefaultValue

    /// 获取所有设定值，如果没有则返回默认值
    member x.DefaultOrValues() =
        x.TryGetRealKey()
        |> Option.map
            (fun kn ->
                cb.GetDefined(kn)
                |> Seq.map (fun item -> x.ConvertValue(item))
                |> Seq.toArray)
        |> Option.defaultValue [| x.DefaultValue |]

type OptionCellSimple<'T when 'T :> IConvertible>(cb : OptionBase, key : string, defValue : 'T) = 
    inherit OptionCell<'T>(cb, key, defValue)

    override x.ConvertValue value = Convert.ChangeType(value, typeof<'T>) :?> 'T

[<AbstractClass>]
type OptionBase() as x =
    static let optCache = Dictionary<string, HashSet<string>>()

    static let seperator = [| ';'; '；'; '：'; ':' |]

    let mutable isParsed = false

    let nonOption = ResizeArray<string>()

    let data =
        Dictionary<string, ResizeArray<string>>(StringComparer.OrdinalIgnoreCase)

    /// 常用选项：指示是否需要文本输出
    member val ResponseType = TextOutputCell(x, "text")

    ///// 常用选项：指示是否需要显示帮助文本
    //member val NeedHelp = OptionCell(x, "help")

    member x.Parsed = isParsed

    member x.IsDefined(key) =
        if not isParsed then invalidOp "尚未解析数据"
        data.ContainsKey(key)

    member x.GetDefined(key) : IReadOnlyList<string> =
        if not isParsed then invalidOp "尚未解析数据"
        data.[key] :> IReadOnlyList<_>

    /// 解析前的前处理操作
    abstract PreParse : string [] -> string []
    default x.PreParse(args) = args

    member x.Parse(cmdArg : CommandEventArgs) = x.Parse(cmdArg.Arguments)

    member x.Parse(input : string []) =
        data.Clear()
        nonOption.Clear()
        isParsed <- false

        let defKeys = x.TryGenerateOptionCache()

        for item in x.PreParse(input) do
            let isOption = item.IndexOfAny(seperator) <> -1

            if isOption then
                let s =
                    item.Split(seperator, 2, StringSplitOptions.RemoveEmptyEntries)

                let key = s.[0]

                if defKeys.Contains(key) then
                    let value = if s.Length >= 2 then s.[1] else ""
                    x.OptAddOrAppend(key, value)
                else
                    nonOption.Add(item)
            else
                nonOption.Add(item)

        isParsed <- true

    member x.NonConfigStrings = nonOption :> IReadOnlyList<_>

    member private x.TryGenerateOptionCache() : HashSet<string> =
        let key = x.GetType().FullName

        if not <| optCache.ContainsKey(key) then
            optCache.[key] <- HashSet<_>(StringComparer.OrdinalIgnoreCase)

            for prop in x.GetType().GetProperties() do
                let pt = prop.PropertyType

                if pt.IsSubclassOf(typeof<OptionCell>) then
                    let cell = prop.GetValue(x) :?> OptionCell

                    if not <| optCache.[key].Add(cell.KeyName) then
                        invalidOp (sprintf "键名已被使用: %s" cell.KeyName)

                    for item in cell.Aliases do
                        if not <| optCache.[key].Add(item) then
                            invalidOp (sprintf "键名已被使用: alias(%s)" item)

        optCache.[key]

    member private x.OptAddOrAppend(key, value) =
        if not <| data.ContainsKey(key) then data.[key] <- ResizeArray<_>()

        data.[key].Add(value)

type TextOutputCell(cb, key) =
    inherit OptionCell(cb, key)

    member x.Value =
        if x.IsDefined then ForceText else PreferImage