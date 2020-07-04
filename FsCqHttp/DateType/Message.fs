namespace KPX.FsCqHttp.DataType.Message

open System
open System.Collections.Generic

open Newtonsoft.Json
open Newtonsoft.Json.Linq

[<RequireQualifiedAccess>]
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

type RawMessageSection = 
    {   Type : string
        Values : IReadOnlyDictionary<string, string> }

    /// 用于直接获取Values内的数据
    member x.Item str = x.Values.[str]
    static member Create(name, vals : seq<string * string>) = 
        {   Type = name
            Values = vals |> readOnlyDict }

[<JsonConverter(typeof<MessageConverter>)>]
type Message(sec : RawMessageSection []) as x = 
    inherit ResizeArray<RawMessageSection>()
    
    static let TYPE_AT   = "at"
    static let TYPE_TEXT = "text"
    static let TYPE_IMAGE= "image"

    static let cqStringReplace = 
        [|
            "&", "&amp;"
            "[", "&#91;"
            "]", "&#93;"
            ",", "&#44;"
        |]

    do
        x.AddRange(sec)

    new () = Message(Array.empty)

    new (sec : RawMessageSection) = Message(Array.singleton sec)

    member x.Add(at : AtUserType) = 
        x.Add(RawMessageSection.Create(TYPE_AT, ["qq", at.ToString()]))

    member x.Add(msg : string) = 
        x.Add(RawMessageSection.Create(TYPE_TEXT, ["text", msg]))

    member x.Add(img : Drawing.Bitmap) = 
        use ms  = new IO.MemoryStream()
        img.Save(ms, Drawing.Imaging.ImageFormat.Jpeg)
        let b64 = Convert.ToBase64String(ms.ToArray(), Base64FormattingOptions.None)
        let segValue= [ "file", ("base64://" + b64) ]
        x.Add(RawMessageSection.Create(TYPE_IMAGE, segValue))


    /// 获取At
    /// 默认忽略at全体成员
    member x.GetAts(?allowAll : bool) =
        let allowAll = defaultArg allowAll false
        [|
            for item in x do 
                if item.Type = TYPE_AT then
                    let at = AtUserType.FromString(item.["qq"])
                    match at with
                    | AtUserType.All -> if allowAll then yield at
                    | AtUserType.User _ -> yield at
        |]

    /// 提取所有文本段为字符串
    override x.ToString() =
        let sb =
            x
            |> Seq.fold (fun (sb : Text.StringBuilder) item ->
                if item.Type = TYPE_TEXT then sb.Append(item.["text"]) |> ignore
                sb) (Text.StringBuilder())
        sb.ToString()

    member x.ToCqString() = 
        let escape (str : string) = 
            let text = Text.StringBuilder(str)
            for (c, esc) in cqStringReplace do
                text.Replace(c, esc) |> ignore
            text.ToString()

        let sb = Text.StringBuilder()
        for sec in x do
            if sec.Type = TYPE_TEXT then
                sb.Append(escape (sec.["text"])) |> ignore
            else
                let args = 
                    sec.Values
                    |> Seq.map (fun kv -> sprintf "%s=%s" kv.Key (escape (kv.Value)))
                sb.AppendFormat("[CQ:{0},", sec.Type)
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
                        yield RawMessageSection.Create(name, args)
                    else
                        yield RawMessageSection.Create(TYPE_TEXT, ["text", decode(seg)])
            |]
        new Message(segs)

and MessageConverter() =
    inherit JsonConverter<Message>()

    override x.WriteJson(w : JsonWriter, r : Message, s : JsonSerializer) =
        w.WriteStartArray()
        for sec in r do
            w.WriteStartObject()
            w.WritePropertyName("type")
            w.WriteValue(sec.Type)
            w.WritePropertyName("data")
            w.WriteStartObject()
            for item in sec.Values do
                w.WritePropertyName(item.Key)
                w.WriteValue(item.Value)
            w.WriteEndObject()
            w.WriteEndObject()
        w.WriteEndArray()

    override x.ReadJson(r : JsonReader, objType : Type, existingValue : Message, hasExistingValue : bool,
                        s : JsonSerializer) =
        
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
                msg.Add(RawMessageSection.Create(t, d))
            msg
        | JsonToken.String ->
            Message.FromCqString(r.ReadAsString())
        | other -> failwithf "未知消息类型:%A --> %O" other r
        