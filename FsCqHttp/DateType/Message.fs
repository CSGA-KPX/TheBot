namespace KPX.FsCqHttp.DataType.Message
open System
open System.Collections.Generic
open Newtonsoft.Json
open Newtonsoft.Json.Linq

type RawMessageFmt = string * IReadOnlyDictionary<string, string>

type MessageSection =
    /// 文本段
    | Text of string
    | Other of RawMessageFmt

    member internal x.ToRaw() : RawMessageFmt =
        match x with
        | Text x -> "text", ([|"text", x|] |> readOnlyDict)
        | Other (n,data) -> n, data

    static member internal FromRaw(raw : RawMessageFmt) =
        match raw with
        | ("text", data) ->
            Text data.["text"]
        | (name, data) ->
            Other (name, data)

[<JsonConverter(typeof<MessageConverter>)>]
type Message(sec : MessageSection[]) as x =
    inherit ResizeArray<MessageSection>()

    do
        x.AddRange(sec)

    new () = 
        new Message([||])

    static member Empty = new Message()

    /// 提取所有文本段为string
    override x.ToString() = 
        let sb = 
            x
            |> Seq.fold (fun (sb : Text.StringBuilder) item -> 
                match item with
                | Text str -> sb.Append(str)
                | _ -> sb) (new Text.StringBuilder())
        sb.ToString()
    
    static member TextMessage(str) =
        let msg = new Message()
        msg.Add(Text str)
        msg

and MessageConverter() =
    inherit JsonConverter<Message>()

    override x.WriteJson(w:JsonWriter , r : Message, s:JsonSerializer) =
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

    override x.ReadJson(r : JsonReader, objType : Type, existingValue : Message, hasExistingValue : bool, s : JsonSerializer) =
        let msg = new Message()

        let arr = JArray.Load(r)
        for sec in arr.Children<JObject>() do
            let t = sec.["type"].Value<string>()
            let d =
                    [|
                        if sec.["data"].HasValues then
                            let child = sec.["data"].Value<JObject>()
                            for p in child.Properties() do
                                yield (p.Name, p.Value.ToString())
                    |] |> readOnlyDict
            msg.Add(MessageSection.FromRaw(t,d))
        msg