module TheBot.Module.SudoModule

open System
open System.IO
open System.Reflection
open System.Security.Cryptography

open KPX.FsCqHttp.Api
open KPX.FsCqHttp.DataType.Response
open KPX.FsCqHttp.DataType.Event
open KPX.FsCqHttp.Handler.CommandHandlerBase

open TheBot.Utils.HandlerUtils

type SudoModule() =
    inherit CommandHandlerBase()
    
    let mutable allow = false
    let mutable isSuUsed = false

    [<CommandHandlerMethodAttribute("#selftest", "(管理) 返回系统信息", "")>]
    member x.HandleSelfTest(msgArg : CommandArgs) =
        failOnNonAdmin(msgArg)
        let caller = msgArg.ApiCaller
        let info =
            "\r\n" + caller.CallApi<SystemApi.GetLoginInfo>().ToString() + "\r\n"
            + caller.CallApi<SystemApi.GetStatus>().ToString() + "\r\n"
            + caller.CallApi<SystemApi.GetVersionInfo>().ToString()
        msgArg.QuickMessageReply(info)

    [<CommandHandlerMethodAttribute("#allow", "(管理) 允许好友、加群请求", "")>]
    member x.HandleAllow(msgArg : CommandArgs) =
        failOnNonAdmin(msgArg)
        msgArg.QuickMessageReply("已允许加群")
        allow <- true

    [<CommandHandlerMethodAttribute("#disallow", "(管理) 禁止好友、加群请求", "")>]
    member x.HandleDisallow(msgArg : CommandArgs) =
        failOnNonAdmin(msgArg)
        msgArg.QuickMessageReply("已关闭加群")
        allow <- false

    [<CommandHandlerMethodAttribute("#rebuildxivdb", "(管理) 重建FF14数据库", "")>]
    member x.HandleRebuildXivDb(msgArg : CommandArgs) =
        failOnNonAdmin(msgArg)
        XivData.Utils.ClearDb()
        msgArg.QuickMessageReply("清空数据库完成")
        XivData.Utils.InitAllDb()
        msgArg.QuickMessageReply("重建数据库完成")

    [<CommandHandlerMethodAttribute("#su", "提交凭据，添加当前用户为超管", "")>]
    member x.HandleSu(msgArg : CommandArgs) = 
        if isSuUsed then failwith "本次认证已被使用"
        let cb = Assembly.GetExecutingAssembly().CodeBase
        let uri = new UriBuilder(cb)
        let path = Uri.UnescapeDataString(uri.Path)
        let md5 = MD5.Create()
        let hex = BitConverter.ToString(md5.ComputeHash(File.OpenRead(path))).Replace("-", "")
        let isMatch = msgArg.RawMessage.ToUpperInvariant().Contains(hex)
        if isMatch then
            addAdmin(msgArg.MessageEvent.UserId)
            setOwner(msgArg.MessageEvent.UserId)
            msgArg.QuickMessageReply("完毕")
        isSuUsed <- true

    [<CommandHandlerMethodAttribute("#grant", "（超管）添加被@用户为管理员", "")>]
    member x.HandleGrant(msgArg : CommandArgs) = 
        if not <| isSenderOwner(msgArg) then failwithf "权限不足"
        let ats = msgArg.MessageEvent.Message.GetAts()
        let sb = Text.StringBuilder()
        for at in ats do
            match at with
            | KPX.FsCqHttp.DataType.Message.AtUserType.User uid ->
                addAdmin(uid)
                sb.AppendLine(sprintf "已添加userId = %i" uid) |> ignore
            | _ -> failwith ""
        msgArg.QuickMessageReply(sb.ToString())

    override x.HandleRequest(args, e) =
        match e with
        | Request.FriendRequest req -> args.SendResponse(FriendAddResponse(allow, ""))
        | Request.GroupRequest req -> args.SendResponse(GroupAddResponse(allow, ""))
