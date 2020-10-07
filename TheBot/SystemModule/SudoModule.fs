module TheBot.Module.SudoModule

open System
open System.IO
open System.Reflection
open System.Security.Cryptography

open KPX.FsCqHttp.Api
open KPX.FsCqHttp.DataType.Response
open KPX.FsCqHttp.DataType.Event
open KPX.FsCqHttp.Handler.CommandHandlerBase
open KPX.FsCqHttp.Utils.TextTable
open KPX.FsCqHttp.Utils.UserOption

open TheBot.Utils.Config
open TheBot.Utils.HandlerUtils

type SudoModule() =
    inherit CommandHandlerBase()
    
    let allowList = Collections.Generic.HashSet<string>()
    let allowQqFmt (self : uint64) (uid : uint64) = sprintf "%i:qq:%i"  self uid
    let allowGroupFmt (self : uint64) (gid : uint64) = sprintf "%i:group:%i"  self gid

    let mutable isSuUsed = false

    [<CommandHandlerMethodAttribute("#selftest", "(超管) 返回系统信息", "", IsHidden = true)>]
    member x.HandleSelfTest(msgArg : CommandArgs) =
        msgArg.EnsureSenderOwner()
        let caller = msgArg.ApiCaller
        let info =
            "\r\n" + caller.CallApi<SystemApi.GetLoginInfo>().ToString() + "\r\n"
            + caller.CallApi<SystemApi.GetStatus>().ToString() + "\r\n"
            + caller.CallApi<SystemApi.GetVersionInfo>().ToString()
        msgArg.QuickMessageReply(info)

    [<CommandHandlerMethodAttribute("#rebuilddatacache", "(超管) 重建数据缓存", "", IsHidden = true)>]
    member x.HandleRebuildXivDb(msgArg : CommandArgs) =
        msgArg.EnsureSenderOwner()
        BotData.Common.Database.BotDataInitializer.ClearCache()
        BotData.Common.Database.BotDataInitializer.ShrinkCache()
        msgArg.QuickMessageReply("清空数据库完成")
        BotData.Common.Database.BotDataInitializer.InitializeAllCollections()
        msgArg.QuickMessageReply("重建数据库完成")

    [<CommandHandlerMethodAttribute("#su", "提交凭据，添加当前用户为超管", "", IsHidden = true)>]
    member x.HandleSu(msgArg : CommandArgs) = 
        if isSuUsed then failwith "本次认证已被使用"
        let cb = Assembly.GetExecutingAssembly().CodeBase
        let uri = new UriBuilder(cb)
        let path = Uri.UnescapeDataString(uri.Path)
        let md5 = MD5.Create()
        let hex = BitConverter.ToString(md5.ComputeHash(File.OpenRead(path))).Replace("-", "")
        let isMatch = msgArg.RawMessage.ToUpperInvariant().Contains(hex)
        if isMatch then
            let uid = msgArg.MessageEvent.UserId
            x.Logger.Info("添加超管和管理员权限{0}", uid)
            msgArg.SetInstanceOwner(uid)
            msgArg.GrantBotAdmin(uid)
            msgArg.QuickMessageReply("完毕")
        isSuUsed <- true

    [<CommandHandlerMethodAttribute("#grant", "（超管）添加用户为管理员", "", IsHidden = true)>]
    member x.HandleGrant(msgArg : CommandArgs) = 
        msgArg.EnsureSenderOwner()

        let uo = UserOptionParser()
        uo.RegisterOption("qq", "0")
        uo.Parse(msgArg.Arguments)

        let uids = uo.GetValues<uint64>("qq")
        let sb = Text.StringBuilder()
        for uid in uids do
            if uid <> 0UL then
                msgArg.GrantBotAdmin(uid)
                sb.AppendLine(sprintf "已添加userId = %i" uid) |> ignore
        msgArg.QuickMessageReply(sb.ToString())

    [<CommandHandlerMethodAttribute("#admins", "（超管）显示当前机器人管理账号", "", IsHidden = true)>]
    member x.HandleShowBotAdmins(msgArg : CommandArgs) = 
        msgArg.EnsureSenderOwner()
        let admins = msgArg.GetBotAdmins()
        let ret = String.Join("\r\n", admins)
        msgArg.QuickMessageReply(ret)

    [<CommandHandlerMethodAttribute("#showgroups", "（超管）检查加群信息", "", IsHidden = true)>]
    member x.HandleShowGroups(msgArg : CommandArgs) = 
        msgArg.EnsureSenderOwner()
        let api = msgArg.ApiCaller.CallApi<SystemApi.GetGroupList>()

        let tt = AutoTextTable<SystemApi.GroupInfo>([|
            "群号", fun (i : SystemApi.GroupInfo) -> box (i.GroupId)
            "名称", fun i -> box (i.GroupName)
            |])

        for g in api.Groups do 
            tt.AddObject(g)
        msgArg.QuickMessageReply(tt.ToString())

    [<CommandHandlerMethodAttribute("#abortall", "（超管）断开所有WS连接", "", IsHidden = true)>]
    member x.HandleShowAbortAll(msgArg : CommandArgs) = 
        msgArg.EnsureSenderOwner()
        for ctx in KPX.FsCqHttp.Instance.CqWsContextPool.Instance do 
            ctx.Stop()

    [<CommandHandlerMethodAttribute("#allow", "(管理) 允许好友、加群请求", "", IsHidden = true)>]
    member x.HandleAllow(msgArg : CommandArgs) =
        let cfg = UserOptionParser()
        cfg.RegisterOption("group", "0")
        cfg.RegisterOption("qq", "0")
        cfg.Parse(msgArg.Arguments)

        if cfg.IsDefined("group") then
            msgArg.EnsureSenderAdmin()
            let key = allowGroupFmt msgArg.SelfId (cfg.GetValue<uint64>("group"))
            allowList.Add(key) |> ignore
            msgArg.QuickMessageReply(sprintf "接受来自[%s]的邀请" key)
        elif cfg.IsDefined("qq") then
            msgArg.EnsureSenderAdmin()
            let key = allowQqFmt msgArg.SelfId (cfg.GetValue<uint64>("friend"))
            allowList.Add(key) |> ignore
            msgArg.QuickMessageReply(sprintf "接受来自[%s]的邀请" key)
        else
            let sb = Text.StringBuilder()
            Printf.bprintf sb "设置群白名单： group:群号\r\n"
            Printf.bprintf sb "设置好友： qq:群号\r\n"
            msgArg.QuickMessageReply(sb.ToString())

    override x.HandleRequest(args, e) =
        match e with
        | Request.FriendRequest req ->
            let inList = allowList.Contains(allowQqFmt args.SelfId req.UserId)
            let isAdmin = args.GetBotAdmins().Contains(req.UserId)
            args.SendResponse(FriendAddResponse(inList || isAdmin, ""))
        | Request.GroupRequest req ->
            let inList = allowList.Contains(allowGroupFmt args.SelfId req.GroupId)
            args.SendResponse(GroupAddResponse(inList, ""))
