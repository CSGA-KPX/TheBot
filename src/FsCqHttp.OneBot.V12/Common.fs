namespace KPX.FsCqHttp.OneBot.V12

open System
open System.Collections.Generic
open Newtonsoft.Json

open FSharp.Reflection


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