namespace rec KPX.FsCqHttp.Message

open System
open System.Collections.Generic

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open KPX.FsCqHttp.Message.Sections


[<JsonConverter(typeof<MessageConverter>)>]
type Message(items : seq<MessageSection>) =
    let sections = ResizeArray<MessageSection>()

    do sections.AddRange(items)

    static let cqStringReplace =
        [| "&", "&amp;"
           "[", "&#91;"
           "]", "&#93;"
           ",", "&#44;" |]

    new() = Message(Seq.empty)

    new(sec : MessageSection) = Message(Seq.singleton sec)

    member x.Count = sections.Count

    member x.Add(sec : MessageSection) = sections.Add(sec)

    member x.Add(at : AtUserType) = x.Add(AtSection.Create(at))

    member x.Add(msg : string) = x.Add(TextSection.Create(msg))

    member x.Add(img : Drawing.Bitmap) = x.Add(ImageSection.Create(img))

    member x.Clear() = sections.Clear()

    member x.Contains(item) = sections.Contains(item)

    member x.Remove(item) = sections.Remove(item)

    member x.GetSections<'T when 'T :> MessageSection>() =
        x
        |> Seq.filter (fun sec -> sec :? 'T)
        |> Seq.map (fun sec -> sec :?> 'T)

    member x.TryGetSection<'T when 'T :> MessageSection>() = x.GetSections<'T>() |> Seq.tryHead

    member x.TryGetAt(?allowAll : bool) =
        let allowAll = defaultArg allowAll false

        x.GetSections<AtSection>()
        |> Seq.tryFind
            (fun atSection ->
                match atSection.At with
                | AtUserType.All -> allowAll
                | AtUserType.User _ -> true)
        |> Option.map (fun atSection -> atSection.At)

    /// 获取At
    /// 默认忽略at全体成员
    member x.GetAts(?allowAll : bool) =
        let allowAll = defaultArg allowAll false

        x.GetSections<AtSection>()
        |> Seq.filter
            (fun atSection ->
                match atSection.At with
                | AtUserType.All -> allowAll
                | AtUserType.User _ -> true)
        |> Seq.map (fun atSection -> atSection.At)

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
            | :? TextSection -> sb.Append(escape (sec.Values.["text"])) |> ignore
            | _ ->
                let args =
                    sec.Values
                    |> Seq.map (fun kv -> sprintf "%s=%s" kv.Key (escape (kv.Value)))

                sb
                    .AppendFormat("[CQ:{0},", sec.TypeName)
                    .Append(String.Join(",", args))
                    .Append("]")
                |> ignore

        sb.ToString()

    static member FromCqString(str : string) =
        let decode = System.Net.WebUtility.HtmlDecode

        let segs =
            [| for seg in str.Split('[', ']') do
                   if seg.StartsWith("CQ") then
                       let segv = seg.Split(',')
                       let name = segv.[0].Split(':').[1]

                       let args =
                           [| for arg in segv.[1..] do
                                  let argv = arg.Split('=')
                                  yield argv.[0], decode (argv.[1]) |]

                       yield MessageSection.CreateFrom(name, args)
                   else
                       yield TextSection.Create(decode (seg)) |]

        new Message(segs)

    interface ICollection<MessageSection> with
        member x.Count = sections.Count

        member x.IsReadOnly = (sections :> ICollection<_>).IsReadOnly

        member x.Add(item) = sections.Add(item)

        member x.Clear() = sections.Clear()

        member x.Contains(item) = sections.Contains(item)

        member x.CopyTo(array, index) = sections.CopyTo(array, index)

        member x.GetEnumerator() =
            (sections :> IEnumerable<_>).GetEnumerator()

        member x.GetEnumerator() =
            (sections :> Collections.IEnumerable)
                .GetEnumerator()

        member x.Remove(item) = sections.Remove(item)

type MessageConverter() =
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

    override x.ReadJson(r : JsonReader, _ : Type, _ : Message, _ : bool, _ : JsonSerializer) =

        match r.TokenType with
        | JsonToken.StartArray ->
            let msg = Message()
            let arr = JArray.Load(r)

            for sec in arr.Children<JObject>() do
                let t = sec.["type"].Value<string>()

                let d =
                    seq {
                        if sec.["data"].HasValues then
                            let child = sec.["data"].Value<JObject>()

                            for p in child.Properties() do
                                yield (p.Name, p.Value.ToString())
                    }

                msg.Add(MessageSection.CreateFrom(t, d))

            msg
        | JsonToken.String -> Message.FromCqString(r.ReadAsString())
        | other -> failwithf "未知消息类型:%A --> %O" other r
