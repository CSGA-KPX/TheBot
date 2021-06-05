namespace KPX.FsCqHttp.Event

open Newtonsoft.Json

open KPX.FsCqHttp.Event.Message


[<CLIMutable>]
type MessageEvent =
    { [<JsonProperty("message_type")>]
      MessageType : string
      [<JsonProperty("sub_type")>]
      SubType : string
      [<JsonProperty("message_id")>]
      MessageId : int64
      [<JsonProperty("user_id")>]
      UserId : uint64
      [<JsonProperty("message")>]
      Message : KPX.FsCqHttp.Message.Message
      [<JsonProperty("raw_message")>]
      RawMessage : string
      [<JsonProperty("font")>]
      Font : int32
      [<JsonProperty("sender")>]
      Sender : Sender

      /// 群号
      /// 群消息专用
      [<JsonProperty("group_id")>]
      GroupId : uint64
      /// 匿名信息，如果不是匿名消息则为 null
      /// 群消息专用
      [<JsonProperty("anonymous")>]
      Anonymous : AnonymousUser

      /// 讨论组号
      /// 讨论组消息专用
      [<JsonProperty("discuss_id")>]
      DiscussId : uint64 }

    member x.IsPrivate = x.MessageType = "private"

    member x.IsGroup = x.MessageType = "group"

    member x.IsDiscuss = x.MessageType = "discuss"

    /// 获取显示的名称
    ///
    /// 私聊和讨论组获取昵称。
    ///
    /// 群聊获取群名片，如果群名片为空使用昵称。
    member x.DisplayName =
        match x with
        | x when x.IsPrivate -> x.Sender.NickName
        | x when x.IsDiscuss -> x.Sender.NickName
        | x when x.IsGroup -> if System.String.IsNullOrEmpty(x.Sender.Card) then x.Sender.NickName else x.Sender.Card
        | _ -> failwithf ""
