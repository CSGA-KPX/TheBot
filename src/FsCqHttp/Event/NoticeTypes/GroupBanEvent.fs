namespace KPX.FsCqHttp.Event.Notice

open Newtonsoft.Json

open KPX.FsCqHttp


[<Struct>]
[<JsonConverter(typeof<StringEnumConverter<GroupBanEventSubtype>>)>]
type GroupBanEventSubtype =
    /// 禁言操作
    | Ban
    /// 解除禁言操作
    | [<AltStringEnumValue("lift_ban")>] LiftBan

/// 群禁言事件
type GroupBanEvent =
    { [<JsonProperty("sub_type")>]
      SubType: GroupBanEventSubtype
      [<JsonProperty("group_id")>]
      GroupId: GroupId
      [<JsonProperty("operator_id")>]
      OperatorId: UserId
      [<JsonProperty("user_id")>]
      /// 被禁言 QQ 号
      UserId: UserId
      [<JsonProperty("duration")>]
      /// 禁言时长，单位秒
      Duration: uint64 }
