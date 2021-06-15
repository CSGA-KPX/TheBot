namespace rec KPX.FsCqHttp

open System
open System.Collections.Generic
open Newtonsoft.Json

open FSharp.Reflection


type SingleCaseInlineConverter<'T>() =
    inherit JsonConverter<'T>()

    static let caseCache =
        FSharpType.GetUnionCases(typeof<'T>, false).[0]

    static let fieldCache = caseCache.GetFields().[0]

    override this.WriteJson(writer : JsonWriter, value : 'T, _ : JsonSerializer) : unit =
        let obj =
            fieldCache.GetMethod.Invoke(value, Array.empty)

        writer.WriteRawValue(obj.ToString())

    override this.ReadJson(reader, objectType, _, _, _) =
        let v = reader.Value :?> string
        let obj = Convert.ChangeType(v, objectType)

        FSharpValue.MakeUnion(caseCache, Array.singleton obj, false) :?> 'T


type StringEnumConverter<'T>() =
    inherit JsonConverter<'T>()

    static let fieldDict =
        Dictionary<string, UnionCaseInfo>(StringComparer.OrdinalIgnoreCase)

    static do
        let fields =
            FSharpType.GetUnionCases(typeof<'T>, false)

        for f in fields do
            fieldDict.Add(f.Name, f)

    override this.WriteJson(writer : JsonWriter, value : 'T, _ : JsonSerializer) : unit =
        let ui, _ =
            FSharpValue.GetUnionFields(value, typeof<'T>)

        writer.WriteRawValue(ui.Name)

    override this.ReadJson(reader, objectType, _, _, _) =
        let v = reader.Value :?> string
        let succ, ui = fieldDict.TryGetValue(v)

        if not succ then
            invalidArg "value" $"%s{v}不是%s{typeof<'T>.FullName}的允许值"

        FSharpValue.MakeUnion(ui, Array.empty, false) :?> 'T

[<Struct>]
[<JsonConverter(typeof<SingleCaseInlineConverter<UserId>>)>]
type UserId =
    | UserId of uint64
    static member Zero = UserId 0UL
    
[<Struct>]
[<JsonConverter(typeof<SingleCaseInlineConverter<GroupId>>)>]
type GroupId =
    | GroupId of uint64
    static member Zero = GroupId 0UL

[<Struct>]
[<JsonConverter(typeof<SingleCaseInlineConverter<MessageId>>)>]
type MessageId =
    | MessageId of int32
    static member Zero = MessageId 0
