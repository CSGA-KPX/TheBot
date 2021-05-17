namespace KPX.TheBot.Module.SudoModule

open System
open System.IO
open System.Reflection
open System.Security.Cryptography

open KPX.FsCqHttp.Api.System
open KPX.FsCqHttp.Api.Group

open KPX.FsCqHttp.Event
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextTable
open KPX.FsCqHttp.Utils.UserOption

open KPX.TheBot.Utils.HandlerUtils

open KPX.TheBot.Data.Common.Database


type SudoModule() =
    inherit CommandHandlerBase()

    let allowList = Collections.Generic.HashSet<string>()
    let allowQqFmt (self : uint64) (uid : uint64) = sprintf "%i:qq:%i" self uid
    let allowGroupFmt (self : uint64) (gid : uint64) = sprintf "%i:group:%i" self gid

    let mutable isSuUsed = false

    [<CommandHandlerMethodAttribute("##selftest", "(超管) 返回系统信息", "", IsHidden = true)>]
    member x.HandleSelfTest(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()
        let caller = cmdArg.ApiCaller

        let info =
            "\r\n"
            + caller.CallApi<GetLoginInfo>().ToString()
            + "\r\n"
            + caller.CallApi<GetStatus>().ToString()
            + "\r\n"
            + caller.CallApi<GetVersionInfo>().ToString()

        cmdArg.Reply(info)

    [<CommandHandlerMethodAttribute("##rebuilddatacache", "(超管) 重建数据缓存", "", IsHidden = true)>]
    member x.HandleRebuildXivDb(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()
        BotDataInitializer.ClearCache()
        BotDataInitializer.ShrinkCache()
        cmdArg.Reply("清空数据库完成")
        BotDataInitializer.InitializeAllCollections()
        cmdArg.Reply("重建数据库完成")

    [<CommandHandlerMethodAttribute("##su", "提交凭据，添加当前用户为超管", "", IsHidden = true)>]
    member x.HandleSu(cmdArg : CommandEventArgs) =
        if isSuUsed then
            cmdArg.Reply("本次认证已被使用")
        else
            let path = Assembly.GetExecutingAssembly().Location
            let md5 = MD5.Create()

            let hex =
                BitConverter
                    .ToString(md5.ComputeHash(File.OpenRead(path)))
                    .Replace("-", "")

            let isMatch =
                cmdArg.RawMessage.ToUpperInvariant().Contains(hex)

            if isMatch then
                let uid = cmdArg.MessageEvent.UserId
                x.Logger.Info("添加超管和管理员权限{0}", uid)
                cmdArg.SetInstanceOwner(uid)
                cmdArg.GrantBotAdmin(uid)
                cmdArg.Reply("完毕")

            isSuUsed <- true

    [<CommandHandlerMethodAttribute("##grant", "（超管）添加用户为管理员", "", IsHidden = true)>]
    member x.HandleGrant(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()

        let uo = OptionBase()
        let qq = uo.RegisterOption("qq", 0UL)
        uo.Parse(cmdArg)

        let sb = Text.StringBuilder()

        for uid in qq.Values do
            if uid <> 0UL then
                cmdArg.GrantBotAdmin(uid)

                sb.AppendLine(sprintf "已添加userId = %i" uid)
                |> ignore

        cmdArg.Reply(sb.ToString())

    [<CommandHandlerMethodAttribute("##admins", "（超管）显示当前机器人管理账号", "", IsHidden = true)>]
    member x.HandleShowBotAdmins(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()
        let admins = cmdArg.GetBotAdmins()
        let ret = String.Join("\r\n", admins)
        cmdArg.Reply(ret)

    [<CommandHandlerMethodAttribute("##showgroups", "（超管）检查加群信息", "", IsHidden = true)>]
    member x.HandleShowGroups(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()
        let api = cmdArg.ApiCaller.CallApi<GetGroupList>()

        let tt = TextTable("群号", "名称")

        for g in api.Groups do
            tt.AddRow(g.GroupId, g.GroupName)

        cmdArg.Reply(tt.ToString())

    [<CommandHandlerMethodAttribute("##abortall", "（超管）断开所有WS连接", "", IsHidden = true)>]
    member x.HandleShowAbortAll(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()

        for ctx in KPX.FsCqHttp.Instance.CqWsContextPool.Instance do
            ctx.Stop()

    [<CommandHandlerMethodAttribute("##allow", "(管理) 允许好友、加群请求", "", IsHidden = true)>]
    member x.HandleAllow(cmdArg : CommandEventArgs) =
        
        let uo = OptionBase()
        let qq = uo.RegisterOption("qq", 0UL)
        let group = uo.RegisterOption("group", 0UL)
        uo.Parse(cmdArg)

        if group.IsDefined then
            cmdArg.EnsureSenderAdmin()

            let key =
                allowGroupFmt cmdArg.BotUserId (group.Value)

            allowList.Add(key) |> ignore
            cmdArg.Reply(sprintf "接受来自[%s]的邀请" key)
        elif qq.IsDefined then
            cmdArg.EnsureSenderAdmin()

            let key =
                allowQqFmt cmdArg.BotUserId (qq.Value)

            allowList.Add(key) |> ignore
            cmdArg.Reply(sprintf "接受来自[%s]的邀请" key)
        else
            let sb = Text.StringBuilder()
            Printf.bprintf sb "设置群白名单： group:群号\r\n"
            Printf.bprintf sb "设置好友： qq:群号\r\n"
            cmdArg.Reply(sb.ToString())

    override x.OnRequest = Some x.HandleRequest

    member x.HandleRequest(args)=
        match args.Event with
        | FriendRequest req ->
            let inList =
                allowList.Contains(allowQqFmt args.BotUserId req.UserId)

            let isAdmin = args.GetBotAdmins().Contains(req.UserId)
            args.Reply(FriendAddResponse(inList || isAdmin, ""))
        | GroupRequest req ->
            let inList =
                allowList.Contains(allowGroupFmt args.BotUserId req.GroupId)

            args.Reply(GroupAddResponse(inList, ""))
