namespace KPX.TheBot.Module.DebugModule

open System
open KPX.FsCqHttp
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Api.Context

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable
open KPX.FsCqHttp.Utils.UserOption

open KPX.TheBot.Utils.HandlerUtils


type DebugModule() =
    inherit CommandHandlerBase()

    //TODO: null检查
    static let nlogMemoryTarget =
        NLog.LogManager.Configuration.FindTargetByName("memory") :?> NLog.Targets.MemoryTarget

    [<CommandHandlerMethod("##showconfig", "(超管) 返回配置信息", "", IsHidden = true)>]
    member x.HandleShowConfig(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()

        let tt = TextTable("名称", "值")

        let cfg = Config

        for p in cfg.GetType().GetProperties() do
            let v = p.GetValue(cfg)

            if v.GetType().IsPrimitive || (v :? String) then
                tt.AddRow(p.Name, $"%A{v}")
            else
                tt.AddRow(p.Name, "{复杂类型}")

        using (cmdArg.OpenResponse(PreferImage)) (fun ret -> ret.Write(tt))

    [<CommandHandlerMethod("##setlog", "(超管) 设置日志设置", "event, api, command", IsHidden = true)>]
    member x.HandleSetLogging(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()

        let cfg = OptionBase()

        let event =
            cfg.RegisterOption("event", Config.LogEventPost)

        let api =
            cfg.RegisterOption("api", Config.LogApiCall)

        let apiJson =
            cfg.RegisterOption("apijson", Config.LogApiJson)

        let command =
            cfg.RegisterOption("command", Config.LogCommandCall)

        cfg.Parse(cmdArg.HeaderArgs)

        Config.LogEventPost <- event.Value
        Config.LogApiCall <- api.Value
        Config.LogApiJson <- apiJson.Value
        Config.LogCommandCall <- command.Value

        let ret =
            $"日志选项已设定为：##setlog event:%b{Config.LogEventPost} api:%b{Config.LogApiCall} apijson:%b{
                                                                                                       Config.LogApiJson
            } command:%b{Config.LogCommandCall}"

        cmdArg.Reply(ret)

    [<CommandHandlerMethod("##showlog", "(超管) 显示日志", "", IsHidden = true)>]
    member x.HandleShowLogging(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()

        let logs = nlogMemoryTarget.Logs

        if logs.Count = 0 then
            cmdArg.Reply("暂无")
        else
            use ret = cmdArg.OpenResponse(PreferImage)

            ret.WriteLine("当前日志有记录{0}条", logs.Count)

            for log in logs do
                ret.WriteLine(log)

            logs.Clear()

    [<CommandHandlerMethod("##cmdtest", "（超管）单元测试", "")>]
    member x.HandleCommandTest(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()

        // 备份测试前的日志信息
        let logs = nlogMemoryTarget.Logs |> Seq.toArray

        try
            let mi =
                cmdArg.ApiCaller.CallApi<GetCtxModuleInfo>()

            mi.ModuleInfo.TestCallbacks
            |> Seq.iter (fun (_, test) -> test.Invoke())

            cmdArg.Reply("成功完成")
        with e -> using (cmdArg.OpenResponse(PreferImage)) (fun ret -> ret.Write $"{e}")

        nlogMemoryTarget.Logs.Clear()

        // 还原日志
        for log in logs do
            nlogMemoryTarget.Logs.Add(log)
