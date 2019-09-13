module SudoModule
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.DataType.Response
open KPX.FsCqHttp.DataType.Event
open KPX.FsCqHttp.Instance.Base

open System
open KPX.FsCqHttp.DataType.Event
open KPX.FsCqHttp.Instance.Base
open CommandHandlerBase
open Utils



let admins =
        [|
            313388419L
            343512452L
        |] |> Set.ofArray



type SudoModule() =
    inherit CommandHandlerBase()
    let mutable allow = false

    [<MessageHandlerMethodAttribute("selftest", "(管理员) 返回系统信息", "")>]
    member x.HandleSelfTest(str : string, arg : ClientEventArgs, msg : Message.MessageEvent) = 
        if admins.Contains(msg.UserId) then
            let info = 
                "\r\n" + 
                    arg.CallApi<SystemApi.GetLoginInfo>().ToString() + "\r\n" + 
                    arg.CallApi<SystemApi.GetStatus>().ToString() + "\r\n" + 
                    arg.CallApi<SystemApi.GetVersionInfo>().ToString()
            arg.QuickMessageReply(info)
        else
            arg.QuickMessageReply("朋友你不是狗管理")

    [<MessageHandlerMethodAttribute("allow", "(管理员) 允许好友、加群请求", "")>]
    member x.HandleAllow(str : string, arg : ClientEventArgs, msg : Message.MessageEvent) = 
        if admins.Contains(msg.UserId) then
            arg.QuickMessageReply("已允许加群")
            allow <- true
        else
            arg.QuickMessageReply("朋友你不是狗管理")

    [<MessageHandlerMethodAttribute("disallow", "(管理员) 禁止好友、加群请求", "")>]
    member x.HandleDisallow(str : string, arg : ClientEventArgs, msg : Message.MessageEvent) = 
        if admins.Contains(msg.UserId) then
            arg.QuickMessageReply( "已关闭加群")
            allow <- false
        else
            arg.QuickMessageReply("朋友你不是狗管理")


    override x.HandleRequest(args, e)=
        match e with
        | Request.FriendRequest x ->
            args.SendResponse(FriendAddResponse(allow, ""))
        | Request.GroupRequest x ->
            args.SendResponse(GroupAddResponse(allow, ""))