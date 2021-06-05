namespace KPX.FsCqHttp.Event.Notice

open System
open Newtonsoft.Json
open Newtonsoft.Json.Linq


/// 群文件信息
type GroupFile =
    { [<JsonProperty("id")>]
      Id : string
      [<JsonProperty("name")>]
      Name : string
      [<JsonProperty("size")>]
      Size : int64
      ///用途不明
      [<JsonProperty("busid")>]
      BusId : int64 }

type GroupUploadEvent =
    { [<JsonProperty("group_id")>]
      GroupId : uint64
      [<JsonProperty("user_id")>]
      UserId : uint64
      [<JsonProperty("file")>]
      File : GroupFile }


type GroupAdminEvent =
    { [<JsonProperty("sub_type")>]
      SubType : string
      [<JsonProperty("group_id")>]
      GroupId : uint64
      [<JsonProperty("user_id")>]
      UserId : uint64 }

    ///设置管理员
    member x.IsSet = x.SubType = "set"

    ///取消管理员
    member x.IsUnset = x.SubType = "unset"


type GroupDecreaseEvent =
    { [<JsonProperty("sub_type")>]
      SubType : string
      [<JsonProperty("group_id")>]
      GroupId : uint64
      [<JsonProperty("operator_id")>]
      OperatorId : uint64
      [<JsonProperty("user_id")>]
      UserId : uint64 }

    /// 成员主动退群
    member x.IsLeave = x.SubType = "leave"

    /// 成员被踢
    member x.IsKick = x.SubType = "kick"

    /// 机器人被踢
    member x.IsKickMe = x.SubType = "kick_me"

type GroupIncreaseEvent =
    { [<JsonProperty("sub_type")>]
      SubType : string
      [<JsonProperty("group_id")>]
      GroupId : uint64
      [<JsonProperty("operator_id")>]
      OperatorId : uint64
      [<JsonProperty("user_id")>]
      UserId : uint64 }

    /// 管理员已同意入群
    member x.IsApprove = x.SubType = "approve"
    /// 管理员邀请入群
    member x.IsInvite = x.SubType = "invite"


type FriendAddEvent =
    { [<JsonProperty("user_id")>]
      UserId : uint64 }

/// 群消息撤回事件
type GroupRecallEvent =
    { [<JsonProperty("group_id")>]
      GroupId : uint64
      [<JsonProperty("user_id")>]
      UserId : uint64
      [<JsonProperty("operator_id")>]
      OperatorId : uint64
      [<JsonProperty("message_id")>]
      MessageId : int64 }

/// 好友消息撤回事件
type FriendRecallEvent =
    { [<JsonProperty("user_id")>]
      UserId : uint64
      [<JsonProperty("message_id")>]
      MessageId : int64 }

/// 群禁言事件
type GroupBanEvent =
    { [<JsonProperty("sub_type")>]
      SubType : string
      [<JsonProperty("group_id")>]
      GroupId : uint64
      [<JsonProperty("operator_id")>]
      OperatorId : uint64
      [<JsonProperty("user_id")>]
      /// 被禁言 QQ 号
      UserId : uint64
      [<JsonProperty("duration")>]
      /// 禁言时长，单位秒
      Duration : uint64 }

    /// 事件为禁言操作
    member x.IsBan = x.SubType = "ban"

    /// 事件为解除禁言操作
    member x.IsLiftBan = x.SubType = "lift_ban"


type HonorType =
    /// 龙王
    | TalkAtive
    /// 群聊之火
    | Performer
    /// 快乐源泉
    | Emotion

/// go-cqhttp增加的一些群通知事件
[<JsonConverter(typeof<GroupNotifyEventConverter>)>]
type GroupNotifyEvent =
    | Poke of GroupId : uint64 * From : uint64 * To : uint64
    /// Owner： 红包发送者，Target：运气王
    | LuckKing of GroupId : uint64 * Owner : uint64 * Target : uint64
    /// 群成员荣誉变更提示
    | Honor of GroupId : uint64 * User : uint64 * Type : HonorType

and GroupNotifyEventConverter() =
    inherit JsonConverter<GroupNotifyEvent>()

    override x.WriteJson(_ : JsonWriter, _ : GroupNotifyEvent, _ : JsonSerializer) =
        raise<unit> <| NotImplementedException()

    override x.ReadJson(r : JsonReader, _ : Type, _ : GroupNotifyEvent, _ : bool, _ : JsonSerializer) =
        let obj = JObject.Load(r)
        let gid = obj.["group_id"].Value<uint64>()
        let uid = obj.["user_id"].Value<uint64>()

        match obj.["sub_type"].Value<string>() with
        | "poke" ->
            let tid = obj.["target_id"].Value<uint64>()
            Poke(gid, uid, tid)
        | "lucky_king" ->
            let tid = obj.["target_id"].Value<uint64>()
            LuckKing(gid, uid, tid)
        | "honor" ->
            let h =
                match obj.["honor_type"].Value<string>() with
                | "talkative" -> TalkAtive
                | "performer" -> Performer
                | "emotion" -> Emotion
                | other ->
                    NLog
                        .LogManager
                        .GetCurrentClassLogger()
                        .Fatal("群成员荣誉类型：{0}", other)

                    raise <| ArgumentOutOfRangeException()

            Honor(gid, uid, h)
        | other ->
            NLog
                .LogManager
                .GetCurrentClassLogger()
                .Fatal("未知群通知事件类型：{0}", other)

            raise<GroupNotifyEvent>
            <| ArgumentOutOfRangeException()


/// 群名片更改时间
type GroupCardEvent =
    { [<JsonProperty("sub_type")>]
      SubType : string
      [<JsonProperty("group_id")>]
      GroupId : uint64
      [<JsonProperty("user_id")>]
      UserId : uint64
      /// 新名片
      [<JsonProperty("card_new")>]
      CardNew : string
      /// 旧名片
      [<JsonProperty("card_old")>]
      CardOld : string }
