namespace KPX.FsCqHttp.Event.Notice

open Newtonsoft.Json

open KPX.FsCqHttp


/// 群消息撤回事件
type GroupRecallEvent =
    { [<JsonProperty("group_id")>]
      GroupId : GroupId
      [<JsonProperty("user_id")>]
      UserId : UserId
      [<JsonProperty("operator_id")>]
      OperatorId : UserId
      [<JsonProperty("message_id")>]
      MessageId : MessageId }