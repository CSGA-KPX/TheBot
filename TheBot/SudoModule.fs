module SudoModule
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.DataType.Response
open KPX.FsCqHttp.DataType.Event
open KPX.FsCqHttp.Instance.Base

let admins =
        [|
            313388419L
            343512452L
        |] |> Set.ofArray



type SudoModule() =
    inherit HandlerModuleBase()
    let mutable allow = false

    override x.MessageHandler _ arg =
        let msg = arg.Data.AsMessageEvent
        let str = msg.Message.ToString()
        match str.ToLowerInvariant() with
        | s when s.StartsWith("#allowrequest") ->
            if admins.Contains(msg.UserId) then
                arg.QuickMessageReply("已允许加群")
                allow <- true
            else
                arg.QuickMessageReply("朋友你不是狗管理")

        | s when s.StartsWith("#disallowrequest") ->
            if admins.Contains(msg.UserId) then
                arg.QuickMessageReply( "已关闭加群")
                allow <- false
            else
                arg.QuickMessageReply("朋友你不是狗管理")
        | s when s.StartsWith("#selftest") ->
            if admins.Contains(arg.Data.AsMessageEvent.UserId) then
                let info = 
                    "\r\n" + 
                        arg.CallApi<SystemApi.GetLoginInfo>().ToString() + "\r\n" + 
                        arg.CallApi<SystemApi.GetStatus>().ToString() + "\r\n" + 
                        arg.CallApi<SystemApi.GetVersionInfo>().ToString()
                arg.QuickMessageReply(info)
            else
                arg.QuickMessageReply("朋友你不是狗管理")
        | _ -> ()

    override x.RequestHandler _ arg =
        match arg.Data.AsRequestEvent with
        | Request.FriendRequest x ->
            arg.SendResponse(FriendAddResponse(allow, ""))
        | Request.GroupRequest x ->
           arg.SendResponse(GroupAddResponse(allow, ""))
