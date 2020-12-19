namespace KPX.FsCqHttp.Message.Sections

open System
open System.Collections.Generic
open System.Reflection

open Newtonsoft.Json.Linq


[<AbstractClass>]
type MessageSection(typeName : string) =

    static let sectionInfoCache =
        let parent = typeof<MessageSection>
        let asm = Assembly.GetExecutingAssembly()

        asm.GetTypes()
        |> Array.filter (fun t -> t.IsSubclassOf(parent) && (not t.IsAbstract))
        |> Array.map
            (fun t ->
                let obj =
                    Activator.CreateInstance(t) :?> MessageSection

                obj.TypeName, t)
        |> readOnlyDict

    let values = Dictionary<string, string>()

    member val Values = values :> IReadOnlyDictionary<_, _>

    /// 该消息段的类型名称
    member x.TypeName = typeName

    member internal x.SetValue(name, value) = values.[name] <- value

    member x.GetValue(name : string) = values.[name]

    member x.TryGetValue(name : string) =
        let succ, item = values.TryGetValue(name)
        if succ then Some item else None


    /// 从指定JObject对象解析消息段
    member internal x.ParseFrom(sec : JObject) =
        let typeName = sec.["type"].Value<string>()

        if (x.TypeName <> "") && (x.TypeName <> typeName)
        then invalidArg "type" (sprintf "type字段不匹配：需求%s，实际%s" x.TypeName typeName)

        if sec.["data"].HasValues then
            let child = sec.["data"].Value<JObject>()

            for p in child.Properties() do
                values.Add(p.Name, p.Value.ToString())

    override x.ToString() =
        let args =
            x.Values
            |> Seq.map (fun a -> sprintf "%s=%s" a.Key a.Value)

        sprintf "[%s:%s]" x.TypeName (String.Join(";", args))

    static member internal CreateFrom(sec : JObject) =
        let mutable typeName = sec.["type"].Value<string>()

        if not <| sectionInfoCache.ContainsKey(typeName) then typeName <- ""

        let t = sectionInfoCache.[typeName]

        let obj =
            Activator.CreateInstance(t) :?> MessageSection

        obj.ParseFrom(sec)
        obj

    static member internal CreateFrom(typeName : string, values : seq<string * string>) =
        let mutable typeName = typeName

        if not <| sectionInfoCache.ContainsKey(typeName) then typeName <- ""

        let t = sectionInfoCache.[typeName]

        let obj =
            Activator.CreateInstance(t) :?> MessageSection

        for (name, value) in values do
            obj.SetValue(name, value)

        obj

/// 用于储存类型未知消息段
type RawMessageSection() =
    inherit MessageSection("")

type TextSection() =
    inherit MessageSection("text")

    member x.Text = x.Values.["text"]

    static member Create(text) =
        MessageSection.CreateFrom("text", [ "text", text ])

type FaceSection() =
    inherit MessageSection("face")

    member x.FaceId = x.Values.["id"]

    static member Create(id : int) =
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

    static member Create(img : Drawing.Bitmap) =
        use ms = new IO.MemoryStream()
        img.Save(ms, Drawing.Imaging.ImageFormat.Jpeg)

        let b64 =
            Convert.ToBase64String(ms.ToArray(), Base64FormattingOptions.None)

        MessageSection.CreateFrom("image", [ "file", ("base64://" + b64) ])

type RecordSection() =
    inherit MessageSection("record")

    member x.File = x.GetValue("file")

    member x.Url = x.TryGetValue("url")

    /// false 默认， true 变声
    member x.RecordType =
        x.TryGetValue("magic")
        |> Option.map (Boolean.Parse)
        |> Option.defaultValue false

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

[<RequireQualifiedAccess>]
type AtUserType =
    | All
    | User of uint64

    override x.ToString() =
        match x with
        | All -> "all"
        | User x -> x |> string

    /// 将CQ码中字符串转换为AtUserType
    static member internal FromString(str : string) =
        if str = "all" then All else User(str |> uint64)

type AtSection() =
    inherit MessageSection("at")

    member x.At = AtUserType.FromString(x.GetValue("qq"))

    static member Create(at : AtUserType) =
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

type JsonSection() =
    inherit MessageSection("json")

    member x.XmlData = x.GetValue("json")
