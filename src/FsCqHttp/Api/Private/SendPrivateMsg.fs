namespace KPX.FsCqHttp.Api.Private

open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Api


type SendPrivateMsg(userId : uint64, message : Message) =
    inherit CqHttpApiBase("send_private_msg")

    override x.WriteParams(w, js) =
        w.WritePropertyName("user_id")
        w.WriteValue(userId)
        w.WritePropertyName("message")
        js.Serialize(w, message)
