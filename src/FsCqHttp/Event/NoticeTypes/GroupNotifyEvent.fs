namespace KPX.FsCqHttp.Event.Notice

open System

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open KPX.FsCqHttp


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
    | Poke of GroupId : GroupId * From : UserId * To : UserId
    /// Owner： 红包发送者，Target：运气王
    | LuckKing of GroupId : GroupId * Owner : UserId * Target : UserId
    /// 群成员荣誉变更提示
    | Honor of GroupId : GroupId * User : UserId * Type : HonorType

and GroupNotifyEventConverter() =
    inherit JsonConverter<GroupNotifyEvent>()

    override x.WriteJson(_ : JsonWriter, _ : GroupNotifyEvent, _ : JsonSerializer) =
        raise<unit> <| NotImplementedException()

    override x.ReadJson(r : JsonReader, _ : Type, _ : GroupNotifyEvent, _ : bool, _ : JsonSerializer) =
        let obj = JObject.Load(r)
        let gid = obj.["group_id"].Value<GroupId>()
        let uid = obj.["user_id"].Value<UserId>()

        match obj.["sub_type"].Value<string>() with
        | "poke" ->
            let tid = obj.["target_id"].Value<UserId>()
            Poke(gid, uid, tid)
        | "lucky_king" ->
            let tid = obj.["target_id"].Value<UserId>()
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