namespace rec KPX.FsCqHttp.OneBot.V12.Message

open System
open System.Collections.Generic
open System.Reflection

open Newtonsoft.Json.Linq

open KPX.FsCqHttp.OneBot.V12
open KPX.FsCqHttp.OneBot.V12.File


/// OneBot消息段
[<AbstractClass>]
type MessageSection(typeName: string) =

    // 缓存当前FsCqHttp中定义的所有消息段
    static let sectionInfoCache =
        let parent = typeof<MessageSection>
        let rawClass = typeof<RawMessageSection>
        let asm = Assembly.GetExecutingAssembly()

        asm.GetTypes()
        |> Array.filter (fun t -> t.IsSubclassOf(parent) && (not t.IsAbstract) && (t <> rawClass))
        |> Array.map (fun t ->
            let obj = Activator.CreateInstance(t) :?> MessageSection

            obj.TypeName, t)
        |> readOnlyDict

    let values = Dictionary<string, string>()

    /// 消息段中所有的项
    member _.Values = values :> IReadOnlyDictionary<_, _>

    /// 该消息段的类型名称
    member _.TypeName = typeName

    /// <summary>
    /// 设定值
    /// </summary>
    /// <param name="name">键名</param>
    /// <param name="value">键值</param>
    member internal _.SetValue(name, value) = values.[name] <- value

    /// <summary>
    /// 获取指定键的值
    ///
    /// 如果不存在抛异常
    /// </summary>
    /// <param name="name">键名</param>
    member _.GetValue(name: string) = values.[name]

    /// <summary>
    /// 尝试获取指定键的值
    /// </summary>
    /// <param name="name">键名</param>
    member _.TryGetValue(name: string) =
        let succ, item = values.TryGetValue(name)
        if succ then Some item else None

    /// 深度复制该消息段
    member x.DeepClone() =
        MessageSection.CreateFrom(x.TypeName, values |> Seq.map (fun kv -> kv.Key, kv.Value))

    /// 从指定JObject对象解析消息段
    member internal x.ParseFrom(sec: JObject) =
        let typeName = sec.["type"].Value<string>()

        if (x.TypeName <> "") && (x.TypeName <> typeName) then
            invalidArg "type" $"type字段不匹配：需求%s{x.TypeName}，实际%s{typeName}"

        if sec.["data"].HasValues then
            let child = sec.["data"].Value<JObject>()

            for p in child.Properties() do
                values.Add(p.Name, p.Value.ToString())

    /// <summary>
    /// 生成调试用信息字符串
    /// </summary>
    override x.ToString() =
        let args = x.Values |> Seq.map (fun a -> $"%s{a.Key}=%s{a.Value}")

        sprintf "[%s:%s]" x.TypeName (String.Join(";", args))

    /// <summary>
    /// 创建消息段
    /// </summary>
    /// <param name="typeName">消息段类型名称</param>
    /// <param name="values">消息段信息</param>
    static member internal CreateFrom(typeName: string, values: seq<string * string>) =
        let mutable typeName = typeName

        if String.IsNullOrEmpty(typeName) then
            invalidArg (nameof typeName) "消息段名称为空"

        let section =
            if sectionInfoCache.ContainsKey(typeName) then
                sectionInfoCache.[typeName] |> Activator.CreateInstance :?> MessageSection
            else
                RawMessageSection(typeName) :> MessageSection

        for name, value in values do
            section.SetValue(name, value)

        section

/// 用于储存类型未知消息段
and RawMessageSection(realTypeName) =
    inherit MessageSection("")

    /// 实际消息段类型
    ///
    /// 覆盖MessageSection.TypeName
    member x.TypeName = realTypeName

type TextSection() =
    inherit MessageSection("text")

    member x.Text = x.Values.["text"]

    static member Create(text) =
        MessageSection.CreateFrom("text", [ "text", text ])

[<RequireQualifiedAccess>]
type AtUserType =
    | All
    | User of UserId

type MentionSection() =
    inherit MessageSection("mention")

    member x.UserId = UserId x.Values.["user_id"]

    member x.At = AtUserType.User x.UserId

    static member Create(userId : UserId) =
        MessageSection.CreateFrom("mention", [ "user_id", userId.Value ])

type MentionAllSection() =
    inherit MessageSection("mention_all")

    member x.At = AtUserType.All

    static member Create() =
        MessageSection.CreateFrom("mention_all", [])

type ImageSection() =
    inherit MessageSection("image")

    member x.Data = RemoteData(FileId x.Values.["file_id"])

    static member Create(data: RemoteData) =
        MessageSection.CreateFrom("image", [ "file_id", data.Id.Value ])

type VoiceSection() =
    inherit MessageSection("voice")

    member x.Data = RemoteData(FileId x.Values.["file_id"])

    static member Create(data: RemoteData) =
        MessageSection.CreateFrom("voice", [ "file_id", data.Id.Value ])

type AudioSection() =
    inherit MessageSection("audio")

    member x.Data = RemoteData(FileId x.Values.["file_id"])

    static member Create(data: RemoteData) =
        MessageSection.CreateFrom("audio", [ "file_id", data.Id.Value ])

type VideoSection() =
    inherit MessageSection("video")

    member x.Data = RemoteData(FileId x.Values.["file_id"])

    static member Create(data: RemoteData) =
        MessageSection.CreateFrom("video", [ "file_id", data.Id.Value ])

type FileSection() =
    inherit MessageSection("file")

    member x.Data = RemoteData(FileId x.Values.["file_id"])

    static member Create(data: RemoteData) =
        MessageSection.CreateFrom("file", [ "file_id", data.Id.Value ])

type LocationSection() =
    inherit MessageSection("location")

    member x.Latitude = float x.Values.["latitude"]
    member x.Longitude = float x.Values.["longitude"]

    member x.Title = x.Values.["title"]
    member x.Content = x.Values.["content"]

    static member Create(lat: float, lon: float, title, content) =
        MessageSection.CreateFrom(
            "location",
            [ "latitude", string lat; "longitude", string lon; "title", title; "content", content ]
        )


type ReplySection() =
    inherit MessageSection("reply")

    member x.MessageId = MessageId x.Values.["message_id"]

    member x.UserId =
        let succ, value = x.Values.TryGetValue("user_id")

        if succ then Some(UserId value) else None
