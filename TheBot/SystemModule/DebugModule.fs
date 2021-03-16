namespace KPX.TheBot.Module.DebugModule

open System
open System.Reflection

open KPX.FsCqHttp
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable
open KPX.FsCqHttp.Utils.UserOption

open KPX.TheBot.Utils.HandlerUtils


type DebugModule() =
    inherit CommandHandlerBase()

    //TODO: null检查
    static let nlogMemoryTarget =
        NLog.LogManager.Configuration.FindTargetByName("memory") :?> NLog.Targets.MemoryTarget

    [<CommandHandlerMethodAttribute("##showconfig", "(超管) 返回配置信息", "", IsHidden = true)>]
    member x.HandleShowConfig(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()

        let cp = typeof<Config.ConfigPlaceholder>

        let prefix = cp.FullName.Replace(cp.Name, "")

        let configTypes =
            typeof<Config.ConfigPlaceholder>
                .Assembly.GetTypes()
            |> Array.filter (fun t -> t.FullName.StartsWith(prefix))

        let tt = TextTable("名称", "值")

        for t in configTypes do
            let ps =
                t.GetProperties(BindingFlags.Static ||| BindingFlags.Public)

            for p in ps do
                let v = p.GetValue(null)
                let pname = sprintf "%s.%s" t.Name p.Name

                if v.GetType().IsPrimitive || (v :? String) then
                    tt.AddRow(pname, sprintf "%A" v)
                else
                    tt.AddRow(pname, "{复杂类型}")

        using (cmdArg.OpenResponse(PreferImage)) (fun ret -> ret.Write(tt))

    [<CommandHandlerMethodAttribute("##setlog", "(超管) 设置日志设置", "event, api, command", IsHidden = true)>]
    member x.HandleSetLogging(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()

        let cfg = UserOptionParser()
        cfg.RegisterOption("event", Config.Logging.LogEventPost.ToString())
        cfg.RegisterOption("api", Config.Logging.LogApiCall.ToString())
        cfg.RegisterOption("apijson", Config.Logging.LogApiJson.ToString())
        cfg.RegisterOption("command", Config.Logging.LogCommandCall.ToString())

        cfg.Parse(cmdArg.Arguments)

        let e = cfg.GetValue<bool>("event")
        let api = cfg.GetValue<bool>("api")
        let apiJson = cfg.GetValue<bool>("apijson")
        let command = cfg.GetValue<bool>("command")

        Config.Logging.LogEventPost <- e
        Config.Logging.LogApiCall <- api
        Config.Logging.LogApiJson <- apiJson
        Config.Logging.LogCommandCall <- command

        let ret =
            sprintf
                "日志选项已设定为：时间(%b) Api(%b) ApiJson(%b) 指令调用(%b)"
                Config.Logging.LogEventPost
                Config.Logging.LogApiCall
                Config.Logging.LogApiJson
                Config.Logging.LogCommandCall

        cmdArg.QuickMessageReply(ret)

    [<CommandHandlerMethodAttribute("##showlog", "(超管) 显示日志", "", IsHidden = true)>]
    member x.HandleShowLogging(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()

        let logs = nlogMemoryTarget.Logs

        if logs.Count = 0 then
            cmdArg.QuickMessageReply("暂无")
        else
            use ret = cmdArg.OpenResponse(PreferImage)

            ret.WriteLine("当前日志有记录{0}条", logs.Count)

            for log in logs do
                ret.WriteLine(log)

            logs.Clear()
