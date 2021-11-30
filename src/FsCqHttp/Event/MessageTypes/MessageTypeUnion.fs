namespace rec KPX.FsCqHttp.Event.Message

open Newtonsoft.Json

open KPX.FsCqHttp


[<RequireQualifiedAccess>]
[<Struct>]
[<JsonConverter(typeof<StringEnumConverter<MessageType>>)>]
/// 消息事件类型
type MessageType =
    /// 私聊消息
    | Private
    /// 群内消息
    | Group
