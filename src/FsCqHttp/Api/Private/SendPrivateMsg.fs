﻿namespace KPX.FsCqHttp.Api.Private

open KPX.FsCqHttp
open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Api


type SendPrivateMsg(userId : UserId, message : ReadOnlyMessage) =
    inherit CqHttpApiBase("send_private_msg")

    override x.WriteParams(w, js) =
        w.WritePropertyName("user_id")
        js.Serialize(w, userId)
        w.WritePropertyName("message")
        js.Serialize(w, message)
