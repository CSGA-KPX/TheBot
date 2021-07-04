namespace rec KPX.FsCqHttp.Event.Message

open Newtonsoft.Json

open KPX.FsCqHttp


[<Struct>]
[<JsonConverter(typeof<StringEnumConverter<PrivateMessageSubtype>>)>]
/// 私聊消息子类型
type PrivateMessageSubtype =
    /// 好友消息
    | Friend
    /// 群临时消息
    | Group
    /// 其他消息
    | Other
