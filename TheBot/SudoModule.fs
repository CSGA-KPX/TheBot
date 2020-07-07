module TheBot.Module.SudoModule

open System
open System.IO
open System.Reflection
open System.Security.Cryptography

open KPX.FsCqHttp.Api
open KPX.FsCqHttp.DataType.Response
open KPX.FsCqHttp.DataType.Event
open KPX.FsCqHttp.Handler.CommandHandlerBase

open TheBot.Utils.TextTable
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

    [<CommandHandlerMethodAttribute("#rebuilddatacache2", "(管理) 重建数据缓存", "")>]
    member x.HandleRebuildXivDb(msgArg : CommandArgs) =
        failOnNonAdmin(msgArg)
        BotData.Common.Database.BotDataInitializer.ClearCache()
        BotData.Common.Database.BotDataInitializer.ShrinkCache()
        msgArg.QuickMessageReply("清空数据库完成")
        BotData.Common.Database.BotDataInitializer.InitializeAllCollections()
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

    [<CommandHandlerMethodAttribute("#showgroups", "（超管）检查加群信息", "")>]
    member x.HandleShowGroups(msgArg : CommandArgs) = 
        if not <| isSenderOwner(msgArg) then failwithf "权限不足"
        let api = msgArg.ApiCaller.CallApi<SystemApi.GetGroupList>()

        let tt = AutoTextTable<SystemApi.GroupInfo>([|
            "群号", fun i -> box (i.GroupId)
            "名称", fun i -> box (i.GroupName)
            |])
        for g in api.Groups do 
            tt.AddObject(g)
        msgArg.QuickMessageReply(tt.ToString())

    override x.HandleRequest(args, e) =
        match e with
        | Request.FriendRequest req -> args.SendResponse(FriendAddResponse(allow, ""))
        | Request.GroupRequest req -> args.SendResponse(GroupAddResponse(allow, ""))
