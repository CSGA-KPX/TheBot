namespace KPX.FsCqHttp.Api.System

open KPX.FsCqHttp.Api


/// 检查是否可以发送图片
type CanSendImage() =
    inherit ApiRequestBase("can_send_image")

    member val Can = false with get, set

    override x.HandleResponse(r) =
        x.Can <- r.Data.["yes"] |> System.Boolean.Parse

/// 检查是否可以发送语音
type CanSendRecord() =
    inherit ApiRequestBase("can_send_record")

    member val Can = false with get, set

    override x.HandleResponse(r) =
        x.Can <- r.Data.["yes"] |> System.Boolean.Parse
