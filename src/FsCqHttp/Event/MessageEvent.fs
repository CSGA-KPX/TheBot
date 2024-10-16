namespace rec KPX.FsCqHttp.Event

open System

open KPX.FsCqHttp
open KPX.FsCqHttp.Event
open KPX.FsCqHttp.Event.Message

open KPX.FsCqHttp.Message
open Newtonsoft.Json
open Newtonsoft.Json.Linq


[<RequireQualifiedAccess>]
[<JsonConverter(typeof<MessageEventConverter>)>]
[<Struct>]
/// 对消息事件的简单包装
/// 提供群事件和私聊事件的共同属性
type MessageEvent =
    | Private of privateMsg: PrivateMessageEvent
    | Group of groupMsg: GroupMessageEvent
    /// 字体
    [<JsonIgnore>]
    member x.Font =
        match x with
        | MessageEvent.Group g -> g.Font
        | MessageEvent.Private p -> p.Font
    /// 消息
    [<JsonIgnore>]
    member x.Message =
        match x with
        | MessageEvent.Group g -> g.Message
        | MessageEvent.Private p -> p.Message
    /// 消息Id
    [<JsonIgnore>]
    member x.MessageId =
        match x with
        | MessageEvent.Group g -> g.MessageId
        | MessageEvent.Private p -> p.MessageId
    /// 上报类型
    [<JsonIgnore>]
    member x.MessageType =
        match x with
        | MessageEvent.Group g -> g.MessageType
        | MessageEvent.Private p -> p.MessageType
    /// 收到事件的机器人 QQ 号
    [<JsonIgnore>]
    member x.SelfId =
        match x with
        | MessageEvent.Group g -> g.SelfId
        | MessageEvent.Private p -> p.SelfId
    /// 事件发生的时间戳
    [<JsonIgnore>]
    member x.Time =
        match x with
        | MessageEvent.Group g -> g.Time
        | MessageEvent.Private p -> p.Time
    /// 发送者 QQ 号
    [<JsonIgnore>]
    member x.UserId =
        match x with
        | MessageEvent.Group g -> g.UserId
        | MessageEvent.Private p -> p.UserId
    /// 显示在屏幕上的昵称
    /// 群消息优先使用群名片
    [<JsonIgnore>]
    member x.DisplayName =
        match x with
        | MessageEvent.Private p -> p.Sender.Nickname
        | MessageEvent.Group g ->
            if g.Anonymous.IsSome then
                // 如果有匿名字段，取匿名名称
                g.Anonymous.Value.Name
            else if String.IsNullOrEmpty(g.Sender.Card) then
                // 如果群名片为空，则取昵称
                g.Sender.Nickname
            else
                // 否则取群名片
                g.Sender.Card

    /// 强制转换为群事件
    /// 非群事件抛出ArgumentException
    member x.AsGroup() =
        match x with
        | MessageEvent.Group g -> g
        | _ -> invalidArg "MessageEvent" "此消息不是群消息"

    /// 强制转换为私聊事件
    /// 非群事件抛出ArgumentException
    member x.AsPrivate() =
        match x with
        | MessageEvent.Private p -> p
        | _ -> invalidArg "MessageEvent" "此消息不是私聊消息"

    /// 使用QuickOperation API应答事件
    member x.Response(msg: ReadOnlyMessage) =
        match x with
        | MessageEvent.Private privMsg -> PrivateMessageResponse(privMsg.UserId, msg)
        | MessageEvent.Group grpMsg -> GroupMessageResponse(grpMsg.GroupId, msg)

    /// 使用QuickOperation API应答事件
    member x.Response(str: string) =
        let msg = Message()
        msg.Add(str)
        x.Response(msg)

/// 包装后MessageEvent的Json转换器
type MessageEventConverter() =
    inherit JsonConverter<MessageEvent>()

    override this.WriteJson(writer: JsonWriter, value: MessageEvent, serializer: JsonSerializer) : unit =
        match value with
        | MessageEvent.Private p -> serializer.Serialize(writer, p)
        | MessageEvent.Group g -> serializer.Serialize(writer, g)

    override this.ReadJson(reader, _, _, _, _) =
        let obj = JObject.Load(reader)

        match obj.["message_type"].ToObject<MessageType>() with
        | MessageType.Group -> obj.ToObject<GroupMessageEvent>() |> MessageEvent.Group
        | MessageType.Private -> obj.ToObject<PrivateMessageEvent>() |> MessageEvent.Private

[<CLIMutable>]
/// 私聊消息事件
type PrivateMessageEvent =
    { [<JsonProperty("time")>]
      Time: int64
      [<JsonProperty("self_id")>]
      SelfId: UserId
      [<JsonProperty("message_type")>]
      MessageType: MessageType
      [<JsonProperty("sub_type")>]
      SubType: PrivateMessageSubtype
      [<JsonProperty("message_id")>]
      MessageId: MessageId
      [<JsonProperty("user_id")>]
      UserId: UserId
      [<JsonProperty("message")>]
      Message: ReadOnlyMessage
      [<JsonProperty("font")>]
      Font: int32
      [<JsonProperty("sender")>]
      Sender: PrivateSender }

[<CLIMutable>]
/// 群消息事件
type GroupMessageEvent =
    { [<JsonProperty("time")>]
      Time: int64
      [<JsonProperty("self_id")>]
      SelfId: UserId
      [<JsonProperty("message_type")>]
      MessageType: MessageType
      [<JsonProperty("sub_type")>]
      SubType: GroupMessageSubtype
      [<JsonProperty("message_id")>]
      MessageId: MessageId
      [<JsonProperty("group_id")>]
      GroupId: GroupId
      [<JsonProperty("user_id")>]
      UserId: UserId
      [<JsonProperty("anonymous")>]
      [<JsonConverter(typeof<AnonymousUserOptionConverter>)>]
      Anonymous: AnonymousUser option
      [<JsonProperty("message")>]
      Message: ReadOnlyMessage
      [<JsonProperty("font")>]
      Font: int32
      [<JsonProperty("sender")>]
      Sender: GroupSender }

    member x.Response
        (
            msg: ReadOnlyMessage
        ) =
        GroupMessageResponse(x.GroupId, msg)

    member x.Response(str: string) =
        let msg = Message()
        msg.Add(str)
        GroupMessageResponse(x.GroupId, msg)
