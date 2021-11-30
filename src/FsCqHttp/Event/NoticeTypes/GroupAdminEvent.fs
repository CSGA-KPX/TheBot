namespace KPX.FsCqHttp.Event.Notice

open Newtonsoft.Json

open KPX.FsCqHttp


[<Struct>]
[<JsonConverter(typeof<StringEnumConverter<GroupAdminEventSubtype>>)>]
type GroupAdminEventSubtype =
    /// 设置管理员
    | Set
    /// 取消管理员
    | Unset

type GroupAdminEvent =
    { [<JsonProperty("sub_type")>]
      SubType: GroupAdminEventSubtype
      [<JsonProperty("group_id")>]
      GroupId: GroupId
      [<JsonProperty("user_id")>]
      UserId: UserId }
