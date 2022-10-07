namespace rec KPX.FsCqHttp.Utils.UserOption

open System
open System.Collections.Generic
open System.Collections.Concurrent

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse


type OptionCell(cb: OptionBase, key: string) =
    /// 该选项的主要键名
    member x.KeyName = key

    /// 指示该选项是否已被设置
    abstract IsDefined: bool

    default x.IsDefined = x.TryGetRealKey().IsSome

    /// 获取或更改该选项的别名
    member val Aliases = Array.empty<string> with get, set

    /// 查找第一个有效的参数名
    member internal x.TryGetRealKey() : string option =
        if cb.IsDefined(key) then
            Some key
        else
            x.Aliases |> Array.tryFind cb.IsDefined

[<AbstractClass>]
type OptionCell<'T>(cb: OptionBase, key: string, defValue: 'T) =
    inherit OptionCell(cb, key)

    /// 获取或更改该选项的默认值
    member val Default = defValue with get, set

    /// 是否从OptionBase的TryIndex中读取值
    member val ArgIndex: int option = None with get, set

    override x.IsDefined =
        let nonOpts: IReadOnlyList<string> = cb.NonOptionStrings
        (x.ArgIndex.IsSome && (x.ArgIndex.Value < nonOpts.Count)) || base.IsDefined

    abstract ConvertValue: string -> 'T

    member private x.ValueSequence =
        // 首先从K:V里读值
        x.TryGetRealKey()
        |> Option.map (fun kn ->
            cb.GetDefined(kn)
            |> Seq.map (fun item ->
                if String.IsNullOrWhiteSpace(item) then
                    x.Default
                else
                    x.ConvertValue(item)))
        // 如果K:V不存在，尝试从ArgIndex读值
        // 如果还是不行，返回默认值
        |> Option.defaultWith (fun () ->
            x.ArgIndex
            |> Option.map (fun idx -> cb.TryIndexed(idx) |> Option.map x.ConvertValue)
            |> Option.flatten
            |> Option.defaultValue x.Default
            |> Seq.singleton)

    /// 获取第一个设定值，如果没有则返回默认值
    member x.Value = x.ValueSequence |> Seq.head

    /// 获取所有设定值，如果没有则返回默认值
    member x.Values = x.ValueSequence |> Seq.toArray

type OptionCellSimple<'T when 'T :> IConvertible>(cb: OptionBase, key: string, defValue: 'T) =
    inherit OptionCell<'T>(cb, key, defValue)

    override x.ConvertValue value =
        Convert.ChangeType(value, typeof<'T>) :?> 'T

[<RequireQualifiedAccess>]
type UndefinedOptionHandling =
    /// 抛出InvalidArg异常
    | Raise
    /// 无视该字段
    | Ignore
    /// 添加到NonOptionStrings
    | AsNonOption

/// 不提供任何默认选项
type OptionBase() =
    static let conOptCache = ConcurrentDictionary<string, HashSet<string>>()

    static let separators = [| ';'; '；'; '：'; ':' |]

    let localOpts = HashSet<string>()

    let mutable isParsed = false

    let nonOption = ResizeArray<string>()

    let data = Dictionary<string, ResizeArray<string>>(StringComparer.OrdinalIgnoreCase)

    member val UndefinedOptionHandling = UndefinedOptionHandling.Raise with get, set

    member x.Parsed = isParsed

    member x.IsDefined(key) =
        if not isParsed then invalidOp "尚未解析数据"
        data.ContainsKey(key)

    member x.GetDefined(key) : IReadOnlyList<string> =
        if not isParsed then invalidOp "尚未解析数据"
        data.[key] :> IReadOnlyList<_>

    member x.RegisterOption(keyName: string) =
        let cell = OptionCell(x, keyName)
        x.RegisterOptionCore(cell)
        cell

    member x.RegisterOption<'T when 'T :> IConvertible>(keyName: string, defValue: 'T) =
        let cell = OptionCellSimple<'T>(x, keyName, defValue)

        x.RegisterOptionCore(cell)
        cell

    member x.RegisterOption<'T when 'T :> OptionCell>(cell: 'T) =
        x.RegisterOptionCore(cell)
        cell

    member private x.RegisterOptionCore(cell: OptionCell) =
        if isParsed then
            invalidOp "不能再解析参数后添加选项"

        localOpts.Add(cell.KeyName) |> ignore

        for alias in cell.Aliases do
            localOpts.Add(alias) |> ignore

    /// 解析前的前处理操作
    abstract PreParse: seq<string> -> seq<string>
    default x.PreParse(args) = args

    member x.Parse(str: string) =
        CommandEventArgs.SplitArguments(str) |> x.Parse

    member x.Parse(input: seq<string>) =
        data.Clear()
        nonOption.Clear()
        isParsed <- false

        let defKeys: HashSet<string> = x.GetOptionKeys()

        for item in x.PreParse(input) do
            let isOption = item.IndexOfAny(separators) <> -1

            if isOption then
                let s = item.Split(separators, 2, StringSplitOptions.RemoveEmptyEntries)

                let key = s.[0]

                if defKeys.Contains(key) then
                    let value = if s.Length >= 2 then s.[1] else ""
                    x.OptAddOrAppend(key, value)
                else
                    match x.UndefinedOptionHandling with
                    | UndefinedOptionHandling.Ignore -> ()
                    | UndefinedOptionHandling.Raise -> invalidArg key "不是有效的选项名称"
                    | UndefinedOptionHandling.AsNonOption -> nonOption.Add(item)
            else
                nonOption.Add(item)

        isParsed <- true

    member x.NonOptionStrings = nonOption :> IReadOnlyList<_>

    member x.GetNonOptionString() = String.Join(' ', x.NonOptionStrings)

    /// 以文本形式转储已经被设定的选项
    member x.DumpDefinedOptions() =
        if not isParsed then invalidOp "没有解析过数据"

        [| for kv in data do
               for value in kv.Value do
                   yield $"{kv.Key}:{value}" |]

    /// 以文本形式转储选项模板，不包含运行时添加的选项。
    member x.DumpTemplate() =
        [| for cell in x.GetOptions() do
               yield $"%s{cell.KeyName}:" |]

    /// 尝试获取OptionStrings指定位置的值
    member x.TryIndexed(idx) =
        if idx >= nonOption.Count then
            None
        else
            Some nonOption.[idx]

    /// 尝试从OptionStrings的指定位置解析参数
    member x.TryIndexed<'T when 'T :> IConvertible>(idx: int, defValue: 'T) =
        x.TryIndexed(idx)
        |> Option.map (fun value -> Convert.ChangeType(value, typeof<'T>) :?> 'T)
        |> Option.defaultValue defValue

    /// 获取已经定义的键名
    member private x.GetOptionKeys() =
        if localOpts.Count = 0 then
            x.TryGenerateOptionCache()
        else
            let defined = x.TryGenerateOptionCache()
            let cap = localOpts.Count + defined.Count
            let ret = HashSet<_>(cap)
            ret.UnionWith(defined)
            ret.UnionWith(localOpts)
            ret

    /// 获取类在设计时定义的选项
    member private x.GetOptions() : seq<OptionCell> =
        seq {
            let flag = Reflection.BindingFlags.Instance ||| Reflection.BindingFlags.NonPublic

            let targetType = typeof<OptionCell>

            // 获取Fields而不是Properties是因为这样可以使用let绑定
            // 隐藏Cell类型。这样可以同时兼容let绑定和member val声明。
            // 可以在下游需要的时候再将let换成member val进行操作。
            for f in x.GetType().GetFields(flag) do
                let ft = f.FieldType

                // 不检查重复值方便下游应用可以
                // 设置同名选项来重写一些行为
                if ft.IsSubclassOf(targetType) || ft = targetType then
                    yield f.GetValue(x) :?> OptionCell
        }

    member private x.TryGenerateOptionCache() =
        let key = x.GetType().FullName

        conOptCache.GetOrAdd(
            key,
            fun _ ->
                let ret = HashSet<string>()

                for cell in x.GetOptions() do
                    ret.Add(cell.KeyName) |> ignore

                    for alias in cell.Aliases do
                        ret.Add(alias) |> ignore

                ret
        )

    member private x.OptAddOrAppend(key, value) =
        if not <| data.ContainsKey(key) then
            data.[key] <- ResizeArray<_>()

        data.[key].Add(value)

/// 命令常用：提供 text选项和ResponseType属性
type CommandOption() as x =
    inherit OptionBase()

    let shouldTextOutput = OptionCell(x, "text")

    /// 根据text参数，返回推定的影响类型
    member x.ResponseType =
        if shouldTextOutput.IsDefined then
            ForceText
        else
            PreferImage
