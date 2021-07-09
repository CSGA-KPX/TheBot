namespace rec KPX.FsCqHttp.Message

open System
open System.Collections.Generic

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open KPX.FsCqHttp.Message.Sections


/// 只读OneBot消息，由一个或多个MessageSection组成
[<JsonConverter(typeof<ReadOnlyMessageConverter>)>]
type ReadOnlyMessage internal (sections : IReadOnlyList<MessageSection>) =

    static let cqStringReplace =
        [| "&", "&amp;"
           "[", "&#91;"
           "]", "&#93;"
           ",", "&#44;" |]

    [<JsonIgnore>]
    /// 消息段数量
    member x.Count = sections.Count

    /// 检查是否含有指定消息段
    member x.Contains(item) =
        sections |> Seq.exists (fun sec -> sec = item)

    /// 获取所有指定类型的消息段
    member x.GetSections<'T when 'T :> MessageSection>() =
        sections
        |> Seq.filter (fun sec -> sec :? 'T)
        |> Seq.map (fun sec -> sec :?> 'T)

    /// 获取指定类型的消息段
    /// 如果没有，返回None
    member x.TryGetSection<'T when 'T :> MessageSection>() = x.GetSections<'T>() |> Seq.tryHead

    /// 返回所有At消息段
    /// 默认不含at全体成员
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
        let sec =
            x.GetSections<TextSection>() |> Seq.toArray

        match sec.Length with
        | 0 -> String.Empty
        | 1 -> sec.[0].Text
        | _ ->
            let sb = Text.StringBuilder()

            for item in sec do
                sb.Append(item.Text) |> ignore

            sb.ToString()

    /// 转换为cq码表示
    /// <remarks>OneBot v12移除</remarks>
    member x.ToCqString() =
        let escape (str : string) =
            let text = Text.StringBuilder(str)

            for c, esc in cqStringReplace do
                text.Replace(c, esc) |> ignore

            text.ToString()

        let sb = Text.StringBuilder()

        for sec in x do
            match sec with
            | :? TextSection -> sb.Append(escape sec.Values.["text"]) |> ignore
            | _ ->
                let args =
                    sec.Values
                    |> Seq.map (fun kv -> $"%s{kv.Key}=%s{escape kv.Value}")

                sb
                    .AppendFormat("[CQ:{0},", sec.TypeName)
                    .Append(String.Join(",", args))
                    .Append("]")
                |> ignore

        sb.ToString()

    /// 克隆该消息和消息段为可读写的Message类型
    member x.CloneMutable() =
        Message(x |> Seq.map (fun sec -> sec.DeepClone()))

    interface IReadOnlyCollection<MessageSection> with
        member x.Count = sections.Count

        member x.GetEnumerator() =
            (sections :> IEnumerable<_>).GetEnumerator()

        member x.GetEnumerator() =
            (sections :> Collections.IEnumerable)
                .GetEnumerator()

/// OneBot消息，由一个或多个MessageSection组成
type Message private (sections : List<_>) =
    inherit ReadOnlyMessage(sections)

    new() = Message(List<_>())

    new(sec : MessageSection) =
        let items = List<_>()
        items.Add(sec)
        Message(items)

    new(sections : seq<MessageSection>) =
        let items = List<_>()
        items.AddRange(sections)
        Message(items)

    /// 添加消息段到末尾
    member x.Add(sec : MessageSection) = sections.Add(sec)

    /// 快速添加At消息段到末尾
    member x.Add(at : AtUserType) = x.Add(AtSection.Create(at))

    /// 快速添加文本消息段到末尾
    member x.Add(msg : string) = x.Add(TextSection.Create(msg))

    /// 快速添加图片消息段到末尾
    member x.Add(img : Drawing.Bitmap) = x.Add(ImageSection.Create(img))

    /// 清空所有消息段
    member x.Clear() = sections.Clear()

    /// 移除指定消息段
    member x.Remove(item) = sections.Remove(item)

    /// 解析cq码
    /// <remarks>OneBot v12移除</remarks>
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
                                  yield argv.[0], decode argv.[1] |]

                       yield MessageSection.CreateFrom(name, args)
                   else
                       yield TextSection.Create(decode seg) |]

        Message(segs)

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

/// ReadOnlyMessage类型的转换器
/// 可以自动识别string和array格式的消息类型
type ReadOnlyMessageConverter() =
    inherit JsonConverter<ReadOnlyMessage>()

    override x.WriteJson(w : JsonWriter, r : ReadOnlyMessage, _ : JsonSerializer) =
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

    override x.ReadJson
        (
            r : JsonReader,
            _ : Type,
            _ : ReadOnlyMessage,
            _ : bool,
            _ : JsonSerializer
        ) =

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
        | JsonToken.String -> Message.FromCqString(r.Value :?> string)
        | other -> failwithf $"未知消息类型:%A{other} --> {r}"
        :> ReadOnlyMessage