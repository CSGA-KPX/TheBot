namespace KPX.FsCqHttp.DataType.Message

open System
open System.Collections.Generic

open Newtonsoft.Json
open Newtonsoft.Json.Linq


[<JsonConverter(typeof<MessageConverter>)>]
type Message(sec : MessageSection []) as x = 
    inherit ResizeArray<MessageSection>()
    
    static let cqStringReplace =  [| "&", "&amp;"
                                     "[", "&#91;"
                                     "]", "&#93;"
                                     ",", "&#44;" |]

    do
        x.AddRange(sec)

    new () = Message(Array.empty)

    new (sec : MessageSection) = Message(Array.singleton sec)

    member x.Add(at : AtUserType) = x.Add(AtSection.Create(at))

    member x.Add(msg : string) = x.Add(TextSection.Create(msg))

    member x.Add(img : Drawing.Bitmap) = x.Add(ImageSection.Create(img))

    member x.GetSections<'T when 'T :> MessageSection>() = 
        [|  for item in x do
                match item with
                | :? 'T as t -> yield t
                | _ -> ()   |]

    /// 获取At
    /// 默认忽略at全体成员
    member x.GetAts(?allowAll : bool) =
        let allowAll = defaultArg allowAll false
        [|
            for sec in x.GetSections<AtSection>() do 
                match sec.At with
                | AtUserType.All -> if allowAll then yield sec.At
                | AtUserType.User _ -> yield sec.At
        |]

    /// 提取所有文本段为字符串
    override x.ToString() =
        let sb = Text.StringBuilder()
        for item in x.GetSections<TextSection>() do
            sb.Append(item.Text) |> ignore
        sb.ToString()

    member x.ToCqString() = 
        let escape (str : string) = 
            let text = Text.StringBuilder(str)
            for (c, esc) in cqStringReplace do
                text.Replace(c, esc) |> ignore
            text.ToString()

        let sb = Text.StringBuilder()
        for sec in x do
            match sec with
            | :? TextSection ->
                sb.Append(escape (sec.Values.["text"])) |> ignore
            | _ ->
                let args = 
                    sec.Values
                    |> Seq.map (fun kv -> sprintf "%s=%s" kv.Key (escape (kv.Value)))
                sb.AppendFormat("[CQ:{0},", sec.TypeName)
                    .Append(String.Join(",", args))
                    .Append("]") |> ignore
        sb.ToString()

    static member FromCqString(str : string) = 
        let decode = System.Net.WebUtility.HtmlDecode
        let segs = 
            [|
                for seg in str.Split('[', ']') do 
                    if seg.StartsWith("CQ") then
                        let segv = seg.Split(',')
                        let name = segv.[0].Split(':').[1]
                        let args = 
                            [|  for arg in segv.[1..] do 
                                    let argv = arg.Split('=')
                                    yield argv.[0], decode(argv.[1])  |]
                        yield MessageSection.CreateFrom(name, args)
                    else
                        yield TextSection.Create(decode(seg))
            |]
        new Message(segs)

and MessageConverter() =
    inherit JsonConverter<Message>()

    override x.WriteJson(w : JsonWriter, r : Message, _ : JsonSerializer) =
        w.WriteStartArray()
        for sec in r do
            w.WriteStartObject()
            w.WritePropertyName("type")
            w.WriteValue(sec.TypeName)
            w.WritePropertyName("data")
            w.WriteStartObject()
            for item in sec.Values do
                w.WritePropertyName(item.Key)
                w.WriteValue(item.Value)
            w.WriteEndObject()
            w.WriteEndObject()
        w.WriteEndArray()

    override x.ReadJson(r : JsonReader, _ : Type, _ : Message, _ : bool,
                        _ : JsonSerializer) =
        
        match r.TokenType with
        | JsonToken.StartArray -> 
            let msg = Message()
            let arr = JArray.Load(r)
            for sec in arr.Children<JObject>() do
                let t = sec.["type"].Value<string>()
                let d =
                    seq { if sec.["data"].HasValues then
                            let child = sec.["data"].Value<JObject>()
                            for p in child.Properties() do
                                yield (p.Name, p.Value.ToString()) }
                msg.Add(MessageSection.CreateFrom(t, d))
            msg
        | JsonToken.String ->
            Message.FromCqString(r.ReadAsString())
        | other -> failwithf "未知消息类型:%A --> %O" other r
        