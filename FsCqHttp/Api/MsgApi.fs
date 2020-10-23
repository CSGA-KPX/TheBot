namespace KPX.FsCqHttp.Api.MsgApi

open KPX.FsCqHttp.DataType.Message
open KPX.FsCqHttp.DataType.Response
open KPX.FsCqHttp.Api

type SendPrivateMsg(userId : uint64, message : Message) =
    inherit ApiRequestBase("send_private_msg")

    override x.WriteParams(w, js) =
        w.WritePropertyName("user_id")
        w.WriteValue(userId)
        w.WritePropertyName("message")
        js.Serialize(w, message)


type SendGroupMsg(groupId : uint64, message : Message) =
    inherit ApiRequestBase("send_group_msg")

    override x.WriteParams(w, js) =
        w.WritePropertyName("group_id")
        w.WriteValue(groupId)
        w.WritePropertyName("message")
        js.Serialize(w, message)

type SendDiscussMsg() =
    inherit ApiRequestBase("send_discuss_msg")

    do raise <| System.NotImplementedException()

type SendMsg() =
    inherit ApiRequestBase("send_msg")

    do raise <| System.NotImplementedException()

type RevokeMsg() =
    inherit ApiRequestBase("delete_msg")

    do raise <| System.NotImplementedException()
