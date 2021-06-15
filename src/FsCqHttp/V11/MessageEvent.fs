namespace rec KPX.FsCqHttp.Event.Test

open System

open KPX.FsCqHttp
open KPX.FsCqHttp.Event
open KPX.FsCqHttp.Event.Message

open KPX.FsCqHttp.Message
open Newtonsoft.Json


[<RequireQualifiedAccess>]
type MessageEvent =
    | Private of PrivateMessageEvent
    | Group of GroupMessageEvent

/// 为群消息和私聊消息提供通用接口
type IMessageEvent =
    /// 事件发生的时间戳
    abstract Time : int64
    /// 收到事件的机器人 QQ 号
    abstract SelfId : UserId
    /// 上报类型
    abstract MessageType : MessageType
    /// 消息
    abstract Message : KPX.FsCqHttp.Message.Message
    /// 消息Id
    abstract MessageId : MessageId
    /// 发送者 QQ 号
    abstract UserId : UserId
    /// 字体
    abstract Font : int32
    /// 显示在屏幕上的昵称
    /// 群消息优先使用群名片
    abstract DisplayName : string
    /// 返回实际消息类型
    abstract ActualEvent : MessageEvent

[<AutoOpen>]
module IMessageEventExtensions =
    type IMessageEvent with
        member x.Response(msg : Message) =
            match x.ActualEvent with
            | MessageEvent.Private _ -> PrivateMessageResponse(msg)
            | MessageEvent.Group _ -> GroupMessageResponse(msg, false, false, false, false, 0)

        member x.Response(str : string) =
            let msg = Message()
            msg.Add(str)
            x.Response(msg)

[<CLIMutable>]
type PrivateMessageEvent =
    { [<JsonProperty("time")>]
      Time : int64
      [<JsonProperty("self_id")>]
      SelfId : UserId
      [<JsonProperty("message_type")>]
      MessageType : MessageType
      [<JsonProperty("sub_type")>]
      SubType : PrivateMessageSubtype
      [<JsonProperty("message_id")>]
      MessageId : MessageId
      [<JsonProperty("user_id")>]
      UserId : UserId
      [<JsonProperty("message")>]
      Message : KPX.FsCqHttp.Message.Message
      [<JsonProperty("font")>]
      Font : int32
      [<JsonProperty("sender")>]
      Sender : PrivateSender }

    interface IMessageEvent with
        member x.Font = x.Font
        member x.Message = x.Message
        member x.MessageId = x.MessageId
        member x.MessageType = x.MessageType
        member x.SelfId = x.SelfId
        member x.Time = x.Time
        member x.UserId = x.UserId
        member x.DisplayName = x.Sender.Nickname
        member x.ActualEvent = MessageEvent.Private x

[<CLIMutable>]
type GroupMessageEvent =
    { [<JsonProperty("time")>]
      Time : int64
      [<JsonProperty("self_id")>]
      SelfId : UserId
      [<JsonProperty("message_type")>]
      MessageType : MessageType
      [<JsonProperty("sub_type")>]
      SubType : GroupMessageSubtype
      [<JsonProperty("message_id")>]
      MessageId : MessageId
      [<JsonProperty("group_id")>]
      GroupId : GroupId
      [<JsonProperty("user_id")>]
      UserId : UserId
      [<JsonProperty("anonymous")>]
      Anonymous : AnonymousUser
      [<JsonProperty("message")>]
      Message : KPX.FsCqHttp.Message.Message
      [<JsonProperty("font")>]
      Font : int32
      [<JsonProperty("sender")>]
      Sender : GroupSender }

    member x.Response
        (
            msg : Message,
            ?atSender : bool,
            ?delete : bool,
            ?kick : bool,
            ?ban : bool,
            ?banDuration : int
        ) =
        let atSender = defaultArg atSender false
        let delete = defaultArg delete false
        let kick = defaultArg kick false
        let ban = defaultArg ban false
        let banDuration = defaultArg banDuration 0
        GroupMessageResponse(msg, atSender, delete, kick, ban, banDuration)

    member x.Response
        (
            str : string,
            ?atSender : bool,
            ?delete : bool,
            ?kick : bool,
            ?ban : bool,
            ?banDuration : int
        ) =
        let atSender = defaultArg atSender false
        let delete = defaultArg delete false
        let kick = defaultArg kick false
        let ban = defaultArg ban false
        let banDuration = defaultArg banDuration 0
        let msg = Message()
        msg.Add(str)
        GroupMessageResponse(msg, atSender, delete, kick, ban, banDuration)

    interface IMessageEvent with
        member x.Font = x.Font
        member x.Message = x.Message
        member x.MessageId = x.MessageId
        member x.MessageType = x.MessageType
        member x.SelfId = x.SelfId
        member x.Time = x.Time
        member x.UserId = x.UserId

        member x.DisplayName =
            if String.IsNullOrEmpty(x.Sender.Card) then
                x.Sender.Nickname
            else
                x.Sender.Card

        member x.ActualEvent = MessageEvent.Group x
