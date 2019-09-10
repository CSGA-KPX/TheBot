module SudoModule
open KPX.TheBot.WebSocket
open KPX.TheBot.WebSocket.Instance

let admins =
        [|
            313388419L
            343512452L
        |] |> Set.ofArray



type SudoModule() =
    inherit HandlerModuleBase()
    let mutable allow = false

    override x.MessageHandler _ arg =
        let str = arg.Data.Message.ToString()
        match str.ToLowerInvariant() with
        | s when s.StartsWith("#allowrequest") ->
            if admins.Contains(arg.Data.UserId) then
                x.QuickMessageReply(arg, "已允许加群")
                allow <- true
            else
                x.QuickMessageReply(arg, "朋友你不是狗管理")

        | s when s.StartsWith("#disallowrequest") ->
            if admins.Contains(arg.Data.UserId) then
                x.QuickMessageReply(arg, "已关闭加群")
                allow <- false
            else
                x.QuickMessageReply(arg, "朋友你不是狗管理")
        | s when s.StartsWith("#selftest") ->
            if admins.Contains(arg.Data.UserId) then
                let info = 
                    "\r\n" + 
                        arg.Sender.CallApi<Api.GetLoginInfo>(new Api.GetLoginInfo()).ToString() + "\r\n" + 
                        arg.Sender.CallApi<Api.GetStatus>(new Api.GetStatus()).ToString() + "\r\n" + 
                        arg.Sender.CallApi<Api.GetVersionInfo>(new Api.GetVersionInfo()).ToString()
                x.QuickMessageReply(arg, info)
            else
                x.QuickMessageReply(arg, "朋友你不是狗管理")
        | _ -> ()

    override x.RequestHandler _ arg =
        match arg.Data with
        | DataType.Event.Request.FriendRequest x ->
            arg.Response <- DataType.Response.FriendAddResponse(allow, "")
        | DataType.Event.Request.GroupRequest x ->
            arg.Response <- DataType.Response.GroupAddResponse(allow, "")
