namespace KPX.FsCqHttp.Event.Notice

open Newtonsoft.Json

open KPX.FsCqHttp


[<Struct>]
[<JsonConverter(typeof<StringEnumConverter<GroupDecreaseEventSubType>>)>]
type GroupDecreaseEventSubType =
    /// 成员主动退群
    | Leave
    /// 成员被踢
    | Kick
    /// 机器人被踢
    | [<AltStringEnumValue("kick_me")>] KickMe

type GroupDecreaseEvent =
    { [<JsonProperty("sub_type")>]
      SubType : GroupDecreaseEventSubType
      [<JsonProperty("group_id")>]
      GroupId : GroupId
      [<JsonProperty("operator_id")>]
      OperatorId : UserId
      [<JsonProperty("user_id")>]
      UserId : UserId }