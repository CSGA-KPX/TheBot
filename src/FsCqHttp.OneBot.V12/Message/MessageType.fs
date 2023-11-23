namespace rec KPX.FsCqHttp.OneBot.V12.Message

open System
open System.Collections.Generic

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open KPX.FsCqHttp.OneBot.V12
open KPX.FsCqHttp.OneBot.V12.File


/// 只读OneBot消息，由一个或多个MessageSection组成
[<JsonConverter(typeof<ReadOnlyMessageConverter>)>]
type ReadOnlyMessage internal (sections: IReadOnlyList<MessageSection>) =
    /// 消息段数量
    [<JsonIgnore>]
    member x.Count = sections.Count

    /// 检查是否含有指定消息段
    member x.Contains(item) =
        sections |> Seq.exists (fun sec -> sec = item)

    /// 获取所有指定类型的消息段
    member x.GetSections<'T when 'T :> MessageSection>() =
        sections |> Seq.filter (fun sec -> sec :? 'T) |> Seq.map (fun sec -> sec :?> 'T)

    /// 获取指定类型的消息段
    /// 如果没有，返回None
    member x.TryGetSection<'T when 'T :> MessageSection>() = x.GetSections<'T>() |> Seq.tryHead

    /// 返回所有At消息段
    /// 默认不含at全体成员
    member x.TryGetAt(?allowAll: bool) =
        x.GetAts(?allowAll = allowAll) |> Seq.tryHead

    /// 获取At
    /// 默认忽略at全体成员
    member x.GetAts(?allowAll: bool) =
        let allowAll = defaultArg allowAll false

        sections
        |> Seq.choose (fun sec ->
            match sec with
            | :? MentionAllSection as sec -> Some(sec.At)
            | :? MentionSection as sec -> Some(sec.At)
            | _ -> None)
        |> Seq.filter (fun at ->
            match at with
            | AtUserType.All -> allowAll
            | AtUserType.User _ -> true)

    /// 提取所有文本段为字符串
    override x.ToString() =
        let sec = x.GetSections<TextSection>() |> Seq.toArray

        match sec.Length with
        | 0 -> String.Empty
        | 1 -> sec.[0].Text
        | _ ->
            let sb = Text.StringBuilder()

            for item in sec do
                sb.Append(item.Text) |> ignore

            sb.ToString()

    /// 克隆该消息和消息段为可读写的Message类型
    member x.CloneMutable() =
        Message(x |> Seq.map (fun sec -> sec.DeepClone()))

    interface IReadOnlyCollection<MessageSection> with
        member x.Count = sections.Count

        member x.GetEnumerator() =
            (sections :> IEnumerable<_>).GetEnumerator()

        member x.GetEnumerator() =
            (sections :> Collections.IEnumerable).GetEnumerator()

/// OneBot消息，由一个或多个MessageSection组成
type Message private (sections: List<_>) =
    inherit ReadOnlyMessage(sections)

    new() = Message(List<_>())

    new(sec: MessageSection) =
        let items = List<_>()
        items.Add(sec)
        Message(items)

    new(sections: seq<MessageSection>) =
        let items = List<_>()
        items.AddRange(sections)
        Message(items)

    /// 添加消息段到末尾
    member x.Add(sec: MessageSection) = sections.Add(sec)

    /// 快速添加At消息段到末尾
    member x.Add(at: AtUserType) =
        match at with
        | AtUserType.All -> x.Add(MentionAllSection.Create())
        | AtUserType.User uid -> x.Add(MentionSection.Create(uid))

    /// 快速添加文本消息段到末尾
    member x.Add(msg: string) = x.Add(TextSection.Create(msg))

    /// 快速添加图片消息段到末尾
    member x.AddImage(data: RemoteData) = x.Add(ImageSection.Create(data))
    /// 清空所有消息段
    member x.Clear() = sections.Clear()

    /// 移除指定消息段
    member x.Remove(item) = sections.Remove(item)

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
            (sections :> Collections.IEnumerable).GetEnumerator()

        member x.Remove(item) = sections.Remove(item)

/// ReadOnlyMessage类型的转换器
/// 可以自动识别string和array格式的消息类型
type ReadOnlyMessageConverter() =
    inherit JsonConverter<ReadOnlyMessage>()

    override x.WriteJson(w: JsonWriter, r: ReadOnlyMessage, _: JsonSerializer) =
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

    override x.ReadJson(r: JsonReader, _: Type, _: ReadOnlyMessage, _: bool, _: JsonSerializer) =

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
        | other -> failwithf $"未知消息类型:%A{other} --> {r}"
        :> ReadOnlyMessage
