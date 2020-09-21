﻿module TheBot.Module.DebugModule

open System
open System.IO
open System.Reflection
open System.Security.Cryptography

open KPX.FsCqHttp
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.DataType.Response
open KPX.FsCqHttp.DataType.Event
open KPX.FsCqHttp.Handler.CommandHandlerBase
open KPX.FsCqHttp.Utils.TextTable
open KPX.FsCqHttp.Utils.UserOption

open TheBot.Utils.Config
open TheBot.Utils.HandlerUtils

type DebugModule() =
    inherit CommandHandlerBase()

    //TODO: null检查
    static let nlogMemoryTarget = NLog.LogManager.Configuration.FindTargetByName("memory") :?> NLog.Targets.MemoryTarget

    [<CommandHandlerMethodAttribute("#showconfig", "(超管) 返回配置信息", "", IsHidden = true)>]
    member x.HandleShowConfig(msgArg : CommandArgs) =
        failOnNonOwner(msgArg)

        let cp = typeof<KPX.FsCqHttp.Config.ConfigPlaceholder>
        let prefix = cp.FullName.Replace(cp.Name, "")
        let configTypes = 
            typeof<KPX.FsCqHttp.Config.ConfigPlaceholder>.Assembly.GetTypes()
            |> Array.filter (fun t -> t.FullName.StartsWith(prefix))

        let tt = TextTable.FromHeader([| "名称"; "值" |])
        
        for t in configTypes do 
            let ps = t.GetProperties(BindingFlags.Static ||| BindingFlags.Public)
            for p in ps do 
                let v = p.GetValue(null)
                let pname = sprintf "%s.%s" t.Name p.Name
                if v.GetType().IsPrimitive || (v :? String) then
                    tt.AddRow(pname, sprintf "%A" v)
                else
                    tt.AddRow(pname, "{复杂类型}")

        using (msgArg.OpenResponse(true)) (fun ret -> ret.Write(tt))

    [<CommandHandlerMethodAttribute("#setlogging", "(超管) 设置日志设置", "event, api, command", IsHidden = true)>]
    member x.HandleSetLogging(msgArg : CommandArgs) = 
        failOnNonOwner(msgArg)

        let cfg = UserOptionParser()
        cfg.RegisterOption("event", "false")
        cfg.RegisterOption("api", "false")
        cfg.RegisterOption("command", "false")

        cfg.Parse(msgArg.Arguments)

        let e = cfg.GetValue<bool>("event")
        let api = cfg.GetValue<bool>("api")
        let command = cfg.GetValue<bool>("command")

        Config.Logging.LogEventPost <- e
        Config.Logging.LogApiCall <- api
        Config.Logging.LogCommandCall <- command

        let ret = sprintf "日志选项已设定为：时间（%b）API（%b）指令调用（%b）"
                    Config.Logging.LogEventPost
                    Config.Logging.LogApiCall
                    Config.Logging.LogCommandCall
        msgArg.QuickMessageReply(ret)

    [<CommandHandlerMethodAttribute("#showlogging", "(超管) 显示日志", "", IsHidden = true)>]
    member x.HandleShowLogging(msgArg : CommandArgs) = 
        failOnNonOwner(msgArg)
        use ret = msgArg.OpenResponse(true)
        
        let logs = nlogMemoryTarget.Logs |> Seq.toArray
        
        ret.WriteLine("当前日志有记录{0}条", logs.Length)

        for log in logs do 
            ret.WriteLine(log)