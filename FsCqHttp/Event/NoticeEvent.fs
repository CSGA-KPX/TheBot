namespace KPX.FsCqHttp.Event

open System
open Newtonsoft.Json
open Newtonsoft.Json.Linq

open KPX.FsCqHttp.Event.Notice


[<JsonConverter(typeof<NoticeEventConverter>)>]
type NoticeEvent =
    | GroupUpload of GroupUploadEvent
    | GroupAdmin of GroupAdminEvent
    | GroupDecrease of GroupDecreaseEvent
    | GroupIncrease of GroupIncreaseEvent
    | GroupRecall of GroupRecallEvent
    | GroupBan of GroupBanEvent
    | FriendAdd of FriendAddEvent
    | FriendRecall of FriendRecallEvent
    | GroupNotify of GroupNotifyEvent
    | GroupCardUpdate of GroupCardEvent

and NoticeEventConverter() =
    inherit JsonConverter<NoticeEvent>()

    override x.WriteJson(_ : JsonWriter, _ : NoticeEvent, _ : JsonSerializer) =
        raise<unit> <| NotImplementedException()

    override x.ReadJson(r : JsonReader, _ : Type, _ : NoticeEvent, _ : bool, _ : JsonSerializer) =
        let obj = JObject.Load(r)

        match obj.["notice_type"].Value<string>() with
        | "group_upload" -> GroupUpload(obj.ToObject<GroupUploadEvent>())
        | "group_admin" -> GroupAdmin(obj.ToObject<GroupAdminEvent>())
        | "group_decrease" -> GroupDecrease(obj.ToObject<GroupDecreaseEvent>())
        | "group_increase" -> GroupIncrease(obj.ToObject<GroupIncreaseEvent>())
        | "group_recall" -> GroupRecall(obj.ToObject<GroupRecallEvent>())
        | "group_ban" -> GroupBan(obj.ToObject<GroupBanEvent>())
        | "friend_add" -> FriendAdd(obj.ToObject<FriendAddEvent>())
        | "friend_recall" -> FriendRecall(obj.ToObject<FriendRecallEvent>())
        | "notify" -> GroupNotify(obj.ToObject<GroupNotifyEvent>())
        | "group_card" -> GroupCardUpdate(obj.ToObject<GroupCardEvent>())
        | other ->
            NLog
                .LogManager
                .GetCurrentClassLogger()
                .Fatal("未知通知类型：{0}", other)

            raise<NoticeEvent>
            <| ArgumentOutOfRangeException()
