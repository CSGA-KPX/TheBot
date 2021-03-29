namespace rec KPX.FsCqHttp.Utils.UserOptionV2

open System
open System.Collections.Generic

open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Utils.TextResponse


type OptionCell(cb : OptionImpl, key : string) =
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
type OptionCell<'T>(cb : OptionImpl, key : string, defValue : 'T) =
    inherit OptionCell(cb, key)

    /// 获取或更改该选项的默认值
    member val DefaultValue = defValue with get, set

    abstract ConvertValue : string -> 'T

    /// 获取第一个设定值，如果没有则返回默认值
    member x.DefaultOrHead =
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
    member x.DefaultOrValues =
        x.TryGetRealKey()
        |> Option.map
            (fun kn ->
                cb.GetDefined(kn)
                |> Seq.map (fun item -> x.ConvertValue(item))
                |> Seq.toArray)
        |> Option.defaultValue [| x.DefaultValue |]

type OptionCellSimple<'T when 'T :> IConvertible>(cb : OptionImpl, key : string, defValue : 'T) =
    inherit OptionCell<'T>(cb, key, defValue)

    override x.ConvertValue value =
        Convert.ChangeType(value, typeof<'T>) :?> 'T

[<AbstractClass>]
type OptionImpl() =
    static let optCache = Dictionary<string, HashSet<string>>()

    static let seperator = [| ';'; '；'; '：'; ':' |]

    let mutable isParsed = false

    let nonOption = ResizeArray<string>()

    let data =
        Dictionary<string, ResizeArray<string>>(StringComparer.OrdinalIgnoreCase)

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

    member x.NonOptionStrings = nonOption :> IReadOnlyList<_>

    member x.GetNonOptionString() = String.Join(' ', x.NonOptionStrings)

    member private x.TryGenerateOptionCache() : HashSet<string> =
        let key = x.GetType().FullName

        if not <| optCache.ContainsKey(key) then
            optCache.[key] <- HashSet<_>(StringComparer.OrdinalIgnoreCase)

            let flags =
                Reflection.BindingFlags.Instance
                ||| Reflection.BindingFlags.NonPublic

            let targetType = typeof<OptionCell>

            // 获取Fields而不是Properties是因为这样可以使用let绑定
            // 隐藏Cell类型。这样可以同时兼容let绑定和member val声明。
            // 可以在下游需要的时候再将let换成member val进行操作。
            for f in x.GetType().GetFields(flags) do
                let ft = f.FieldType

                if ft.IsSubclassOf(targetType) || ft = targetType then
                    // 不检查重复值意味着下游应用可以
                    // 设置同名选项来重写一些行为
                    let cell = f.GetValue(x) :?> OptionCell
                    optCache.[key].Add(cell.KeyName) |> ignore

                    for item in cell.Aliases do
                        optCache.[key].Add(item) |> ignore

        optCache.[key]

    member private x.OptAddOrAppend(key, value) =
        if not <| data.ContainsKey(key) then
            data.[key] <- ResizeArray<_>()

        data.[key].Add(value)

type OptionBase() as x =
    inherit OptionImpl()

    let shouldTextOutput = OptionCell(x, "text")

    member x.ResponseType =
        if shouldTextOutput.IsDefined then
            ForceText
        else
            PreferImage
