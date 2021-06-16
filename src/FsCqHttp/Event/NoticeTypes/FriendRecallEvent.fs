namespace KPX.FsCqHttp.Event.Notice

open Newtonsoft.Json

open KPX.FsCqHttp


/// 好友消息撤回事件
type FriendRecallEvent =
    { [<JsonProperty("user_id")>]
      UserId : UserId
      [<JsonProperty("message_id")>]
      MessageId : MessageId }
