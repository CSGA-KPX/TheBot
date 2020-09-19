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

    let mutable isSuUsed = false

    [<CommandHandlerMethodAttribute("#selftest", "(管理) 返回系统信息", "", IsHidden = true)>]
    member x.HandleSelfTest(msgArg : CommandArgs) =
        failOnNonAdmin(msgArg)
        let caller = msgArg.ApiCaller
        let info =
            "\r\n" + caller.CallApi<SystemApi.GetLoginInfo>().ToString() + "\r\n"
            + caller.CallApi<SystemApi.GetStatus>().ToString() + "\r\n"
            + caller.CallApi<SystemApi.GetVersionInfo>().ToString()
        msgArg.QuickMessageReply(info)

    [<CommandHandlerMethodAttribute("#allow", "(管理) 允许好友、加群请求", "", IsHidden = true)>]
    member x.HandleAllow(msgArg : CommandArgs) =
        let currentModeKey = sprintf "IsAutoAllow:%i" msgArg.SelfId
        let isAutoAllow = ConfigManager.SystemConfig.Get(currentModeKey, false)

        let cfg = UserOptionParser()
        cfg.RegisterOption("group", "")
        cfg.RegisterOption("qq", "")
        cfg.RegisterOption("autoallow", "")
        cfg.Parse(msgArg.Arguments)

        if cfg.IsDefined("autoallow") then
            if not <| isSenderOwner(msgArg) then failwithf "权限不足"
            let auto = cfg.GetValue<bool>("autoallow")
            ConfigManager.SystemConfig.Put(currentModeKey, auto)
            msgArg.QuickMessageReply(sprintf "自动接受模式设置为:%A" auto)
        elif cfg.IsDefined("group") then
            failOnNonAdmin(msgArg)
            let key = sprintf "group:%i" (cfg.GetValue<uint64>("group"))
            allowList.Add(key) |> ignore
            msgArg.QuickMessageReply(sprintf "接受来自[%s]的邀请" key)
        elif cfg.IsDefined("qq") then
            failOnNonAdmin(msgArg)
            let key = sprintf "qq:%i" (cfg.GetValue<uint64>("friend"))
            allowList.Add(key) |> ignore
            msgArg.QuickMessageReply(sprintf "接受来自[%s]的邀请" key)
        else
            let sb = Text.StringBuilder()
            Printf.bprintf sb "当前机器人自动接受模式为：%A\r\n" isAutoAllow
            Printf.bprintf sb "设置群白名单： group:群号\r\n"
            Printf.bprintf sb "设置好友： qq:群号\r\n"
            Printf.bprintf sb "设置自动接受： autoallow:true/false （需要超管）"
            msgArg.QuickMessageReply(sb.ToString())

    [<CommandHandlerMethodAttribute("#rebuilddatacache", "(管理) 重建数据缓存", "", IsHidden = true)>]
    member x.HandleRebuildXivDb(msgArg : CommandArgs) =
        failOnNonAdmin(msgArg)
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
            addAdmin(msgArg.MessageEvent.UserId)
            setOwner(msgArg.MessageEvent.UserId)
            msgArg.QuickMessageReply("完毕")
        isSuUsed <- true

    [<CommandHandlerMethodAttribute("#grant", "（超管）添加被@用户为管理员", "", IsHidden = true)>]
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

    [<CommandHandlerMethodAttribute("#showgroups", "（超管）检查加群信息", "", IsHidden = true)>]
    member x.HandleShowGroups(msgArg : CommandArgs) = 
        if not <| isSenderOwner(msgArg) then failwithf "权限不足"
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
        if not <| isSenderOwner(msgArg) then failwithf "权限不足"
        for ctx in KPX.FsCqHttp.Instance.CqWsContextPool.Instance do 
            ctx.Stop()

    override x.HandleRequest(args, e) =
        let currentModeKey = sprintf "IsAutoAllow:%i" args.SelfId
        let isAutoAllow = ConfigManager.SystemConfig.Get(currentModeKey, false)

        match e with
        | Request.FriendRequest req ->
            let key = sprintf "qq:%i" req.UserId
            args.SendResponse(FriendAddResponse(allowList.Contains(key) || isAutoAllow, ""))
        | Request.GroupRequest req ->
            let key = sprintf "group:%i" req.GroupId
            args.SendResponse(GroupAddResponse(allowList.Contains(key) || isAutoAllow, ""))
