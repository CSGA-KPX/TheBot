namespace KPX.FsCqHttp.Event.Request

open KPX.FsCqHttp
open KPX.FsCqHttp.Event

open Newtonsoft.Json


[<Struct>]
[<JsonConverter(typeof<StringEnumConverter<GroupRequestEventSubtype>>)>]
type GroupRequestEventSubtype =
    /// 加群申请
    | Add
    /// 邀请申请
    | Invite

type GroupRequestEvent =
    { [<JsonProperty("sub_type")>]
      SubType : GroupRequestEventSubtype
      [<JsonProperty("group_id")>]
      GroupId : GroupId
      [<JsonProperty("user_id")>]
      UserId : UserId
      [<JsonProperty("comment")>]
      Comment : string
      [<JsonProperty("flag")>]
      Flag : string }
    
    member x.Response(approve : bool, ?reason : string) =
        GroupAddResponse(approve, (defaultArg reason ""))