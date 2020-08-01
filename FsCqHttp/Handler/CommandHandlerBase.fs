namespace KPX.FsCqHttp.Handler.CommandHandlerBase

open System
open KPX.FsCqHttp.DataType.Event
open KPX.FsCqHttp.Handler

[<AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)>]
type CommandHandlerMethodAttribute(command : string, desc, lh) =
    inherit Attribute()

    /// 指令名称，不含CommandStart字符串
    member val Command : string = command.ToLowerInvariant()
    /// 指令概述
    member x.HelpDesc : string = desc
    /// 完整帮助文本
    member x.LongHelp : string = lh
    /// 指示改指令是否在help等指令中隐藏
    member val IsHidden = false with get, set

type CommandArgs(msg : Message.MessageEvent, cqArg : ClientEventArgs) =
    inherit ClientEventArgs(cqArg.ApiCaller, cqArg.RawEvent)

    let rawMsg = msg.Message.ToString()
    let cmdLine = rawMsg.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)
    let isCmd = rawMsg.StartsWith(KPX.FsCqHttp.Config.Command.CommandStart)

    let cmdName = 
        if isCmd then
            let cmd = cmdLine.[0].ToLowerInvariant()
            Some (cmd.[1..])
        else
            None

    /// 原始消息对象
    member x.MessageEvent = msg

    /// 原始消息文本
    member x.RawMessage = rawMsg

    /// 空格拆分后的所有字符串
    member x.CommandLine = cmdLine

    /// 小写转化后的命令名称，不含CommandStart字符
    member x.CommandName = cmdName

    /// 检查是否是指定指令
    member x.IsCommand(str : string) = x.CommandName.Value = (str.ToLowerInvariant())

    member val Arguments = [| if isCmd then yield! cmdLine.[1..] |]

[<AbstractClass>]
type CommandHandlerBase(shared : bool) as x =
    inherit HandlerModuleBase(shared)

    /// 声明为共享模块
    new () = CommandHandlerBase(true)

    member val Commands = [| for method in x.GetType().GetMethods() do
                                 let ret = method.GetCustomAttributes(typeof<CommandHandlerMethodAttribute>, true)
                                 for attrib in ret do
                                     let attrib = attrib :?> CommandHandlerMethodAttribute
                                     yield attrib, method |]

    override x.HandleMessage(args, msgEvent) =
        let msgArg = CommandArgs(msgEvent, args)
        if msgArg.CommandName.IsSome then
            let matched = x.Commands |> Array.filter (fun (a, _) -> msgArg.IsCommand(a.Command))
            for (_, method) in matched do
                if KPX.FsCqHttp.Config.Logging.LogCommandCall then
                    x.Logger.Info("Calling handler {0}\r\n Command Context {1}", method.Name, sprintf "%A" msgEvent)
                try
                    method.Invoke(x, [| msgArg |]) |> ignore
                with
                | :? Reflection.TargetInvocationException as e -> 
                    x.Logger.Fatal("Exception caught: {0}", e.ToString())
                    args.QuickMessageReply(sprintf "发生错误：%s" (e.InnerException.Message))
