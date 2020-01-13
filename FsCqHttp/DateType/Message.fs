namespace KPX.FsCqHttp.DataType.Message

open System
open System.Collections.Generic
open Newtonsoft.Json
open Newtonsoft.Json.Linq

type RawMessageFmt = string * IReadOnlyDictionary<string, string>

type AtUserType =
    | All
    | User of uint64

    override x.ToString() =
        match x with
        | All -> "all"
        | User x -> x |> string

    static member FromString(str : string) =
        if str = "all" then All
        else User(str |> uint64)

type MessageSection =
    /// 文本段
    | Text of string
    | At of AtUserType
    | Other of RawMessageFmt

    member internal x.ToRaw() : RawMessageFmt =
        match x with
        | At x -> "at", ([| "qq", x.ToString() |] |> readOnlyDict)
        | Text x -> "text", ([| "text", x |] |> readOnlyDict)
        | Other(n, data) -> n, data

    static member internal FromRaw(raw : RawMessageFmt) =
        match raw with
        | ("at", data) -> At(AtUserType.FromString(data.["qq"]))
        | ("text", data) -> Text data.["text"]
        | (name, data) -> Other(name, data)

[<JsonConverter(typeof<MessageConverter>)>]
type Message(sec : MessageSection []) as x =
    inherit ResizeArray<MessageSection>()

    static let cqStringReplace = 
        [|
            "&", "&amp;"
            "[", "&#91;"
            "]", "&#93;"
            ",", "&#44;"
        |]

    do x.AddRange(sec)

    new() = Message([||])

    static member Empty = Message()

    /// 获取At
    /// 默认不处理at全体成员
    member x.GetAts(?allowAll : bool) =
        let allowAll = defaultArg allowAll false
        [| yield! x.FindAll(fun x ->
                   match x with
                   | MessageSection.At t ->
                       match t with
                       | User x -> true
                       | All when allowAll -> true
                       | All -> false
                   | _ -> false).ConvertAll(fun x ->
                   match x with
                   | MessageSection.At t -> t
                   | _ -> failwith "") |]

    //转换为cq上报字符串
    member x.ToCqString() = 
        let sb = Text.StringBuilder()
        for sec in x do 
            match sec with
            | Text str ->
                let mutable str = str
                for (c, esc) in cqStringReplace.[0..2] do 
                    str <- str.Replace(c, esc)
                sb.Append(str) |> ignore
            | other ->
                let (cmd, arg) = other.ToRaw()
                let args = 
                    arg
                    |> Seq.map (fun kv ->
                        let key = kv.Key
                        let mutable str = kv.Value
                        for (c, esc) in cqStringReplace do 
                            str <- str.Replace(c, esc)
                        sprintf "%s=%s" key str)
                sb.AppendFormat("[CQ:{0},", cmd)
                  .Append(String.Join(",", args))
                  .Append("]") |> ignore
        sb.ToString()

    /// 提取所有文本段为string
    override x.ToString() =
        let sb =
            x
            |> Seq.fold (fun (sb : Text.StringBuilder) item ->
                match item with
                | Text str -> sb.Append(str)
                | _ -> sb) (Text.StringBuilder())
        sb.ToString()

    static member TextMessage(str) =
        let msg = Message()
        msg.Add(Text str)
        msg

and MessageConverter() =
    inherit JsonConverter<Message>()

    override x.WriteJson(w : JsonWriter, r : Message, s : JsonSerializer) =
        w.WriteStartArray()
        for sec in r do
            let (name, data) = sec.ToRaw()
            w.WriteStartObject()
            w.WritePropertyName("type")
            w.WriteValue(name)
            w.WritePropertyName("data")
            w.WriteStartObject()
            for item in data do
                w.WritePropertyName(item.Key)
                w.WriteValue(item.Value)
            w.WriteEndObject()
            w.WriteEndObject()
        w.WriteEndArray()

    override x.ReadJson(r : JsonReader, objType : Type, existingValue : Message, hasExistingValue : bool,
                        s : JsonSerializer) =
        let msg = Message()

        let arr = JArray.Load(r)
        for sec in arr.Children<JObject>() do
            let t = sec.["type"].Value<string>()

            let d =
                [| if sec.["data"].HasValues then
                    let child = sec.["data"].Value<JObject>()
                    for p in child.Properties() do
                        yield (p.Name, p.Value.ToString()) |]
                |> readOnlyDict
            msg.Add(MessageSection.FromRaw(t, d))
        msg
