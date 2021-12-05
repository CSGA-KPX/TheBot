namespace KPX.FsCqHttp.Message.Sections

open System
open System.Collections.Generic
open System.Reflection

open Newtonsoft.Json.Linq

open SkiaSharp

open KPX.FsCqHttp.Message


[<AbstractClass>]
type MessageSection(typeName: string) =

    static let sectionInfoCache =
        let parent = typeof<MessageSection>
        let rawClass = typeof<RawMessageSection>
        let asm = Assembly.GetExecutingAssembly()

        asm.GetTypes()
        |> Array.filter (fun t -> t.IsSubclassOf(parent) && (not t.IsAbstract) && (t <> rawClass))
        |> Array.map
            (fun t ->
                let obj = Activator.CreateInstance(t) :?> MessageSection

                obj.TypeName, t)
        |> readOnlyDict

    let values = Dictionary<string, string>()

    member val Values = values :> IReadOnlyDictionary<_, _>

    /// 该消息段的类型名称
    member x.TypeName = typeName

    member internal x.SetValue(name, value) = values.[name] <- value

    member x.GetValue(name: string) = values.[name]

    member x.TryGetValue(name: string) =
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

    override x.ToString() =
        let args = x.Values |> Seq.map (fun a -> $"%s{a.Key}=%s{a.Value}")

        sprintf "[%s:%s]" x.TypeName (String.Join(";", args))

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

type FaceSection() =
    inherit MessageSection("face")

    member x.FaceId = x.Values.["id"]

    static member Create(id: int) =
        MessageSection.CreateFrom("face", [ "id", id.ToString() ])

type ImageSection() =
    inherit MessageSection("image")

    member x.File = x.GetValue("file")

    member x.Url = x.TryGetValue("url")

    /// None 普通图片， flash 闪照
    member x.ImageType = x.TryGetValue("type")

    /// 未实现
    member x.Cache = raise<bool> <| NotImplementedException()

    /// 未实现
    member x.Proxy = raise<bool> <| NotImplementedException()

    /// 未实现
    member x.Timeout = raise<int> <| NotImplementedException()

    static member Create(img: SKImage) =
        let data = img.Encode(SKEncodedImageFormat.Png, 70).AsSpan()
        let b64 = Convert.ToBase64String(data, Base64FormattingOptions.None)

        MessageSection.CreateFrom("image", [ "file", ("base64://" + b64) ])

type RecordSection() =
    inherit MessageSection("record")

    member x.File = x.GetValue("file")

    member x.Url = x.TryGetValue("url")

    /// false 默认， true 变声
    member x.RecordType = x.TryGetValue("magic") |> Option.map Boolean.Parse |> Option.defaultValue false

    /// 未实现
    member x.Cache = raise<bool> <| NotImplementedException()

    /// 未实现
    member x.Proxy = raise<bool> <| NotImplementedException()

    /// 未实现
    member x.Timeout = raise<int> <| NotImplementedException()

type VideoSection() =
    inherit MessageSection("video")

    member x.File = x.GetValue("file")

    member x.Url = x.TryGetValue("url")

    /// 未实现
    member x.Cache = raise<bool> <| NotImplementedException()

    /// 未实现
    member x.Proxy = raise<bool> <| NotImplementedException()

    /// 未实现
    member x.Timeout = raise<int> <| NotImplementedException()

type AtSection() =
    inherit MessageSection("at")

    member x.At = AtUserType.FromString(x.GetValue("qq"))

    static member Create(at: AtUserType) =
        MessageSection.CreateFrom("at", [ "qq", at.ToString() ])

/// 掷骰子魔法表情
type DiceSection() =
    inherit MessageSection("dice")

/// 猜拳魔法表情
type RpsSection() =
    inherit MessageSection("rps")

/// 窗口抖动
type ShakeSection() =
    inherit MessageSection("rps")

/// 戳一戳，请参考Mirai的HummerMessage.kt
type PokeSection() =
    inherit MessageSection("poke")

    member x.PokeType = x.GetValue("type") |> int32

    member x.PokeId = x.GetValue("id") |> int32

    member x.Name = x.TryGetValue("name")

/// 发送专用，无意义
type AnonymousSection() =
    inherit MessageSection("anonymous")

    /// 表示无法匿名时是否继续发送
    member x.Ignore = x.TryGetValue("ignore")

type ShareSection() =
    inherit MessageSection("share")

    member x.Url = x.GetValue("url")

    member x.Title = x.GetValue("title")

    member x.Content = x.TryGetValue("content")

    member x.Image = x.TryGetValue("image")

/// 推荐群
type ContactSection() =
    inherit MessageSection("contact")

type LocationSection() =
    inherit MessageSection("location")

    member x.Lat = x.GetValue("lat") |> float

    member x.Lon = x.GetValue("lon") |> float

    member x.Title = x.TryGetValue("title")

    member x.Content = x.TryGetValue("content")

/// 未实现
// TODO : 需要考虑自定义分享和一般分享
type MusicShareSection() =
    inherit MessageSection("music")

    /// qq 163 xm custom
    member x.Type = x.GetValue("type")

    member x.MusicId = x.GetValue("id")

type ForwardSection() =
    inherit MessageSection("forward")

    member x.ForwardMessageId = x.GetValue("id")

/// 未实现
type NodeForwardSection() =
    inherit MessageSection("node")

    member x.ForwardMessageId = x.GetValue("id")


type XmlSection() =
    inherit MessageSection("xml")

    member x.XmlData = x.GetValue("data")

    member x.ResId = x.TryGetValue("resid")

type JsonSection() =
    inherit MessageSection("json")

    // 默认不填为0, 走小程序通道, 填了走富文本通道发送
    member x.ResId = x.TryGetValue("resid")

    member x.JsonData = x.GetValue("data")

    member x.GetObject() = JObject.Parse(x.JsonData)
