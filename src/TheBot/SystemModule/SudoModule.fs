namespace KPX.TheBot.Module.SudoModule

open System
open System.IO
open System.Security.Cryptography

open KPX.FsCqHttp.Api.Group
open KPX.FsCqHttp.Api.Context

open KPX.FsCqHttp.Event
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextTable
open KPX.FsCqHttp.Utils.UserOption

open KPX.TheBot.Utils.HandlerUtils

open KPX.TheBot.Data.Common.Resource
open KPX.TheBot.Data.Common.Database


type SudoModule() =
    inherit CommandHandlerBase()

    let allowList = Collections.Generic.HashSet<string>()
    let allowQqFmt (self : uint64) (uid : uint64) = $"%i{self}:qq:%i{uid}"
    let allowGroupFmt (self : uint64) (gid : uint64) = $"%i{self}:group:%i{gid}"

    let mutable isSuUsed = false

    [<CommandHandlerMethod("##rebuilddatacache", "(超管) 重建数据缓存", "", IsHidden = true)>]
    member x.HandleRebuildXivDb(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()
        BotDataInitializer.ClearCache()
        BotDataInitializer.ShrinkCache()
        cmdArg.Reply("清空数据库完成")
        BotDataInitializer.InitializeAllCollections()
        cmdArg.Reply("重建数据库完成")

    [<CommandHandlerMethod("##su", "提交凭据，添加当前用户为超管", "", IsHidden = true)>]
    member x.HandleSu(cmdArg : CommandEventArgs) =
        if isSuUsed then
            cmdArg.Reply("本次认证已被使用")
        else
            let file = GetStaticFile("su.txt")
            
            if File.Exists(file) then
                let data = File.ReadAllBytes(file)
                let hex =
                    BitConverter
                        .ToString(SHA256.Create().ComputeHash(data))
                        .Replace("-", "")

                let isMatch =
                    cmdArg.HeaderLine.ToUpperInvariant().Contains(hex)

                if isMatch then
                    let uid = cmdArg.MessageEvent.UserId
                    x.Logger.Info("添加超管和管理员权限{0}", uid)
                    cmdArg.SetInstanceOwner(uid)
                    cmdArg.GrantBotAdmin(uid)
                    cmdArg.Reply("完毕")
                
                File.Delete(file)
                isSuUsed <- true
            else

                File.WriteAllText(file, Guid.NewGuid().ToString())
                cmdArg.Reply($"请提供SHA256(%s{file})")

    [<CommandHandlerMethod("##grant", "（超管）添加用户为管理员", "", IsHidden = true)>]
    member x.HandleGrant(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()

        let uo = CommandOption()
        let qq = uo.RegisterOption("qq", 0UL)
        uo.Parse(cmdArg.HeaderArgs)

        let sb = Text.StringBuilder()

        for uid in qq.Values do
            if uid <> 0UL then
                cmdArg.GrantBotAdmin(uid)

                sb.AppendLine $"已添加userId = %i{uid}" |> ignore

        cmdArg.Reply(sb.ToString())

    [<CommandHandlerMethod("##admins", "（超管）显示当前机器人管理账号", "", IsHidden = true)>]
    member x.HandleShowBotAdmins(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()
        let admins = cmdArg.GetBotAdmins()
        let ret = String.Join("\r\n", admins)
        cmdArg.Reply(ret)

    [<CommandHandlerMethod("##showgroups", "（超管）检查加群信息", "", IsHidden = true)>]
    member x.HandleShowGroups(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()
        let api = cmdArg.ApiCaller.CallApi<GetGroupList>()

        let tt = TextTable("群号", "名称")

        for g in api.Groups do
            tt.AddRow(g.GroupId, g.GroupName)

        cmdArg.Reply(tt.ToString())

    [<CommandHandlerMethod("##abortall", "（超管）断开所有WS连接", "", IsHidden = true)>]
    member x.HandleShowAbortAll(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()

        for ctx in KPX.FsCqHttp.Instance.CqWsContextPool.Instance do
            ctx.Stop()

    [<CommandHandlerMethod("##allow", "(管理) 允许好友、加群请求", "", IsHidden = true)>]
    member x.HandleAllow(cmdArg : CommandEventArgs) =

        let uo = CommandOption()
        let qq = uo.RegisterOption("qq", 0UL)
        let group = uo.RegisterOption("group", 0UL)
        uo.Parse(cmdArg.HeaderArgs)

        if group.IsDefined then
            cmdArg.EnsureSenderAdmin()

            let key =
                allowGroupFmt cmdArg.BotUserId group.Value

            allowList.Add(key) |> ignore
            cmdArg.Reply $"接受来自[%s{key}]的邀请"
        elif qq.IsDefined then
            cmdArg.EnsureSenderAdmin()

            let key = allowQqFmt cmdArg.BotUserId qq.Value

            allowList.Add(key) |> ignore
            cmdArg.Reply $"接受来自[%s{key}]的邀请"
        else
            let sb = Text.StringBuilder()
            Printf.bprintf sb "设置群白名单： group:群号\r\n"
            Printf.bprintf sb "设置好友： qq:群号\r\n"
            cmdArg.Reply(sb.ToString())

    [<CommandHandlerMethod("##紧急停止", "停止所有指令和事件处理", "", IsHidden = true)>]
    member x.HandleShutdown(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()

        let m =
            cmdArg
                .ApiCaller
                .CallApi<GetCtxModuleInfo>()
                .ModuleInfo

        m.MetaCallbacks.Clear()
        m.NoticeCallbacks.Clear()
        m.RequestCallbacks.Clear()
        m.TestCallbacks.Clear()
        m.MessageCallbacks.Clear()

        let act =
            Action<CommandEventArgs>(fun cmdArg -> cmdArg.Reply("Bot故障：信息处理已禁用"))

        for kv in m.Commands do
            m.Commands.[kv.Key] <- { kv.Value with MethodAction = act }

        x.Logger.Fatal("已完成紧急停止操作")
        cmdArg.Reply("已完成紧急停止操作")

    [<CommandHandlerMethod("##combo", "一次执行多个命令", "", IsHidden = true)>]
    member x.HandleCombo(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderAdmin()
        
        let api = RewriteCommand(cmdArg, cmdArg.AllLines)
        
        cmdArg.ApiCaller.CallApi(api) |> ignore
    
    override x.OnRequest = Some x.HandleRequest

    member x.HandleRequest(args) =
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
