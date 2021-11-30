namespace KPX.FsCqHttp.Event.Notice

open Newtonsoft.Json

open KPX.FsCqHttp



[<Struct>]
[<JsonConverter(typeof<StringEnumConverter<GroupIncreaseEventSubtype>>)>]
type GroupIncreaseEventSubtype =
    /// 管理员已同意入群
    | Approve
    /// 管理员邀请入群
    | Invite

type GroupIncreaseEvent =
    { [<JsonProperty("sub_type")>]
      SubType: GroupIncreaseEventSubtype
      [<JsonProperty("group_id")>]
      GroupId: GroupId
      [<JsonProperty("operator_id")>]
      OperatorId: UserId
      [<JsonProperty("user_id")>]
      UserId: UserId }
