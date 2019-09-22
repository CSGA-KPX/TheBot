namespace KPX.FsCqHttp.Api.MsgApi
open KPX.FsCqHttp.DataType.Message
open KPX.FsCqHttp.DataType.Response
open KPX.FsCqHttp.Api

type SendPrivateMsg() = 
    inherit ApiRequestBase("send_private_msg")

    do
        raise <| System.NotImplementedException()


type SendGroupMsg() = 
    inherit ApiRequestBase("send_group_msg")
    
    do
        raise <| System.NotImplementedException()

type SendDiscussMsg() = 
    inherit ApiRequestBase("send_discuss_msg")
    
    do
        raise <| System.NotImplementedException()

type SendMsg() = 
    inherit ApiRequestBase("send_msg")
    
    do
        raise <| System.NotImplementedException()

type RevokeMsg() = 
    inherit ApiRequestBase("delete_msg")
    
    do
        raise <| System.NotImplementedException()
