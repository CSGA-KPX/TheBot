namespace rec KPX.FsCqHttp

open System
open System.Collections.Generic
open Newtonsoft.Json

open FSharp.Reflection


/// 提供该特性的框架规格
type FeatureSpecification =
    | Unknown = -1
    | OneBot = 0
    | FsCqHttp = 1
    | GoCqHttp = 2

/// 标记特性的相关信息
/// 运行过程中不使用，用来标记信息方便人工检查。
type FeatureAnnotationAttribute(source, ver, reviewed) =
    inherit Attribute()

    /// 特性的规格信息
    member x.Source: FeatureSpecification = source

    /// 规格版本
    member x.SourceVersion: string = ver

    /// 最后复核日期
    member x.Reviewed: string = reviewed

/// 当前规范不含有需求的特性
type FeatureNotSupportedException(msg) =
    inherit Exception(msg)

/// Json字符串值到DU的转换器
type SingleCaseInlineConverter<'T>() =
    inherit JsonConverter<'T>()

    static let caseCache = FSharpType.GetUnionCases(typeof<'T>, false).[0]

    static let fieldCache = caseCache.GetFields().[0]

    override _.WriteJson(writer: JsonWriter, value: 'T, _: JsonSerializer) : unit =
        let obj = fieldCache.GetMethod.Invoke(value, Array.empty)

        writer.WriteValue(obj.ToString())

    override _.ReadJson(reader, _, _, _, _) =
        let obj = Convert.ChangeType(reader.Value, fieldCache.PropertyType)

        FSharpValue.MakeUnion(caseCache, Array.singleton obj, false) :?> 'T

/// 字符串枚举类型DU中不美观值进行重命名
type AltStringEnumValue(value: string) =
    inherit Attribute()

    member x.Value = value

/// 对字符串枚举类型DU的Json转换器
type StringEnumConverter<'T>() =
    inherit JsonConverter<'T>()

    static let fieldDict = Dictionary<string, UnionCaseInfo>(StringComparer.OrdinalIgnoreCase)

    static do
        let fields = FSharpType.GetUnionCases(typeof<'T>, false)

        for f in fields do
            let attrs = f.GetCustomAttributes(typeof<AltStringEnumValue>)

            if attrs.Length = 0 then
                fieldDict.Add(f.Name, f)
            else
                let n = (attrs.[0] :?> AltStringEnumValue).Value
                fieldDict.Add(n, f)

    override _.WriteJson(writer: JsonWriter, value: 'T, _: JsonSerializer) : unit =
        let ui, _ = FSharpValue.GetUnionFields(value, typeof<'T>)

        let attrs = ui.GetCustomAttributes(typeof<AltStringEnumValue>)

        if attrs.Length = 0 then
            writer.WriteValue(ui.Name)
        else
            writer.WriteValue((attrs.[0] :?> AltStringEnumValue).Value)

    override _.ReadJson(reader, _, _, _, _) =
        let v = reader.Value :?> string
        let succ, ui = fieldDict.TryGetValue(v)

        if not succ then
            invalidArg "value" $"%s{v}不是%s{typeof<'T>.FullName}的允许值"

        FSharpValue.MakeUnion(ui, Array.empty, false) :?> 'T

[<Struct>]
[<JsonConverter(typeof<SingleCaseInlineConverter<UserId>>)>]
/// 对OneBot中UserId的包装
/// V12可能会调整为String
type UserId =
    | UserId of uint64
    static member Zero = UserId 0UL

    member x.Value =
        let (UserId value) = x
        value

[<Struct>]
[<JsonConverter(typeof<SingleCaseInlineConverter<GroupId>>)>]
/// 对OneBot中GroupId的包装
/// V12可能会调整为String
type GroupId =
    | GroupId of uint64
    static member Zero = GroupId 0UL

    member x.Value =
        let (GroupId value) = x
        value

[<Struct>]
[<JsonConverter(typeof<SingleCaseInlineConverter<MessageId>>)>]
/// 对OneBot中MessageId的包装
type MessageId =
    | MessageId of int32
    static member Zero = MessageId 0

    member x.Value =
        let (MessageId value) = x
        value

[<Sealed>]
/// 包装OneBot上报的Json对象
/// 同时提供缓存的ToString()方法避免多次求值
type PostContent(ctx: Linq.JObject) =

    let str = lazy (ctx.ToString(Formatting.Indented))

    member x.RawEventPost = ctx

    /// 懒惰求值的字符串
    override x.ToString() = str.Force()
