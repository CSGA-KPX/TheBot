namespace rec KPX.FsCqHttp.Event.Message

open Newtonsoft.Json


[<CLIMutable>]
/// 私聊消息发送者信息
type PrivateSender =
    { [<JsonProperty("nickname")>]
      Nickname : string
      [<JsonProperty("sex")>]
      Sex : string
      [<JsonProperty("age")>]
      Age : int }