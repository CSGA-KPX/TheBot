module TheBot.Module.SudoModule

open KPX.FsCqHttp.Api
open KPX.FsCqHttp.DataType.Response
open KPX.FsCqHttp.DataType.Event
open KPX.FsCqHttp.Handler.CommandHandlerBase
open TheBot.Utils.HandlerUtils

type SudoModule() =
    inherit CommandHandlerBase()
    let mutable allow = false

    [<CommandHandlerMethodAttribute("#selftest", "(管理员) 返回系统信息", "")>]
    member x.HandleSelfTest(msgArg : CommandArgs) =
        failOnNonAdmin(msgArg)
        let caller = msgArg.CqEventArgs.ApiCaller
        let info =
            "\r\n" + caller.CallApi<SystemApi.GetLoginInfo>().ToString() + "\r\n"
            + caller.CallApi<SystemApi.GetStatus>().ToString() + "\r\n"
            + caller.CallApi<SystemApi.GetVersionInfo>().ToString()
        msgArg.CqEventArgs.QuickMessageReply(info)

    [<CommandHandlerMethodAttribute("#allow", "(管理员) 允许好友、加群请求", "")>]
    member x.HandleAllow(msgArg : CommandArgs) =
        failOnNonAdmin(msgArg)
        msgArg.CqEventArgs.QuickMessageReply("已允许加群")
        allow <- true

    [<CommandHandlerMethodAttribute("#disallow", "(管理员) 禁止好友、加群请求", "")>]
    member x.HandleDisallow(msgArg : CommandArgs) =
        failOnNonAdmin(msgArg)
        msgArg.CqEventArgs.QuickMessageReply("已关闭加群")
        allow <- false

    [<CommandHandlerMethodAttribute("#rebuildxivdb", "(管理员) 重建FF14数据库", "")>]
    member x.HandleRebuildXivDb(msgArg : CommandArgs) =
        failOnNonAdmin(msgArg)
        XivData.Utils.ClearDb()
        XivData.Utils.InitAllDb()
        msgArg.CqEventArgs.QuickMessageReply("已完成重建")

    override x.HandleRequest(args, e) =
        match e with
        | Request.FriendRequest x -> args.SendResponse(FriendAddResponse(allow, ""))
        | Request.GroupRequest x -> args.SendResponse(GroupAddResponse(allow, ""))
