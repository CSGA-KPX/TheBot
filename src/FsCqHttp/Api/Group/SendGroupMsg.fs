namespace KPX.FsCqHttp.Api.Group

open KPX.FsCqHttp
open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Api


type SendGroupMsg(groupId : GroupId, message : ReadOnlyMessage) =
    inherit CqHttpApiBase("send_group_msg")

    override x.WriteParams(w, js) =
        w.WritePropertyName("group_id")
        js.Serialize(w, groupId)
        w.WritePropertyName("message")
        js.Serialize(w, message)
