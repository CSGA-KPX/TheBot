namespace KPX.FsCqHttp.DataType.Event.Notice
open System
open Newtonsoft.Json
open Newtonsoft.Json.Linq

type GroupFile = 
    {
        [<JsonProperty("id")>]
        Id    : string
        [<JsonProperty("name")>]
        Name  : string
        [<JsonProperty("size")>]
        Size  : int64
        ///用途不明
        [<JsonProperty("busid")>]
        BusId : int64
    }

type GroupUploadEvent = 
    {
        [<JsonProperty("group_id")>]
        GroupId : int64
        [<JsonProperty("user_id")>]
        UserId  : int64
        [<JsonProperty("file")>]
        File    : GroupFile
    }


type GroupAdminEvent = 
    {
        [<JsonProperty("sub_type")>]
        SubType : string
        [<JsonProperty("group_id")>]
        GroupId : int64
        [<JsonProperty("user_id")>]
        UserId  : int64
    }

    ///设置管理员
    member x.IsSet = x.SubType = "set"

    ///取消管理员
    member x.IsUnset = x.SubType = "unset"


type GroupDecreaseEvent =
    {
        [<JsonProperty("sub_type")>]
        SubType     : string
        [<JsonProperty("group_id")>]
        GroupId     : int64
        [<JsonProperty("operator_id")>]
        OperatorId  : int64
        [<JsonProperty("user_id")>]
        UserId      : int64
    }

    /// 成员主动退群
    member x.IsLeave = x.SubType = "leave"

    /// 成员被踢
    member x.IsKick  = x.SubType = "kick"

    /// 机器人被踢
    member x.IsKickMe = x.SubType = "kick_me"

type GroupIncreaseEvent =
    {
        [<JsonProperty("sub_type")>]
        SubType     : string
        [<JsonProperty("group_id")>]
        GroupId     : int64
        [<JsonProperty("operator_id")>]
        OperatorId  : int64
        [<JsonProperty("user_id")>]
        UserId      : int64
    }

    /// 管理员已同意入群
    member x.IsApprove = x.SubType = "approve"
    /// 管理员邀请入群
    member x.IsInvite  = x.SubType = "invite"


type FriendAddEvent = 
    {
        [<JsonProperty("user_id")>]
        UserId      : int64
    }

[<JsonConverter(typeof<NoticeEventConverter>)>]
type NoticeEvent = 
    | GroupUpload   of GroupUploadEvent
    | GroupAdmin    of GroupAdminEvent
    | GroupDecrease of GroupDecreaseEvent
    | GroupIncrease of GroupIncreaseEvent
    | FriendAdd     of FriendAddEvent

and NoticeEventConverter() = 
    inherit JsonConverter<NoticeEvent>()

    override x.WriteJson(w:JsonWriter , r : NoticeEvent, js:JsonSerializer) =
        raise<unit> <| NotImplementedException()

    override x.ReadJson(r : JsonReader, objType : Type, existingValue : NoticeEvent, hasExistingValue : bool, s : JsonSerializer) =
        let obj = JObject.Load(r)

        match obj.["notice_type"].Value<string>() with
        | "group_upload" -> GroupUpload (obj.ToObject<GroupUploadEvent>())
        | "group_admin"  -> GroupAdmin  (obj.ToObject<GroupAdminEvent>())
        | "group_decrease"  -> GroupDecrease  (obj.ToObject<GroupDecreaseEvent>())
        | "group_increase" -> GroupIncrease  (obj.ToObject<GroupIncreaseEvent>())
        | "friend_add" -> FriendAdd  (obj.ToObject<FriendAddEvent>())
        | other -> 
            NLog.LogManager.GetCurrentClassLogger().Fatal("未知通知类型：{0}", other)
            raise<NoticeEvent> <| ArgumentOutOfRangeException()