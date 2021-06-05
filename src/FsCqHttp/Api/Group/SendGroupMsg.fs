namespace KPX.FsCqHttp.Api.Group

open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Api


type SendGroupMsg(groupId : uint64, message : Message) =
    inherit CqHttpApiBase("send_group_msg")

    override x.WriteParams(w, js) =
        w.WritePropertyName("group_id")
        w.WriteValue(groupId)
        w.WritePropertyName("message")
        js.Serialize(w, message)
