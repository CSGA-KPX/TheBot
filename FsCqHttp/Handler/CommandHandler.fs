namespace KPX.FsCqHttp.Handler

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

    /// 使用自定义的指令起始符，默认为""
    member val AltCommandStart = "" with get, set

    /// 获取该指令的指令起始符
    member x.CommandStart = 
        if String.IsNullOrWhiteSpace(x.AltCommandStart) then
            KPX.FsCqHttp.Config.Command.CommandStart
        else
            x.AltCommandStart

// TODO: 处理AltCommandStart
type CommandArgs(cqArg : ClientEventArgs, msg : Message.MessageEvent, attr : CommandHandlerMethodAttribute) =
    inherit ClientEventArgs(cqArg.ApiCaller, cqArg.RawEvent)

    let rawMsg = msg.Message.ToString()
    let cmdLine = rawMsg.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)

    let cmdName = 
        let cmd = cmdLine.[0].ToLowerInvariant()
        let idx = cmd.IndexOf(attr.CommandStart)
        let len = attr.CommandStart.Length
        cmd.[idx + len .. ]

    /// 原始消息对象
    member x.MessageEvent = msg

    /// 原始消息文本
    member x.RawMessage = rawMsg

    /// 空格拆分后的所有字符串
    member x.CommandLine = cmdLine

    /// 小写转化后的命令名称，不含CommandStart字符
    member x.CommandName = cmdName


    member x.CommandAttrib = attr

    /// 不包含指令的部分
    member val Arguments = cmdLine.[1..]

[<AbstractClass>]
type CommandHandlerBase() as x =
    inherit HandlerModuleBase()

    let cmdCache = Collections.Generic.Dictionary<string, _>()

    do
        for method in x.GetType().GetMethods() do
            let ret = method.GetCustomAttributes(typeof<CommandHandlerMethodAttribute>, true)
            for attrib in ret do
                let attr = attrib :?> CommandHandlerMethodAttribute
                let cs = attr.CommandStart
                let key = (cs + attr.Command).ToLowerInvariant()
                cmdCache.Add(key, (attr, method))

    override x.HandleMessage(args, msgEvent) =
        let cmd = 
            let msg = msgEvent.Message.ToString()
            let endIdx =
                let idx = msg.IndexOf(" ")
                if idx = -1 then msg.Length
                else
                    idx
            // 空格-1，msg.Length变换为idx也需要-1
            let key = msg.[0 .. endIdx - 1].ToLowerInvariant()
            let succ, obj = cmdCache.TryGetValue(key)
            if succ then Some obj else None

        if cmd.IsSome then
            let (attr, method) = cmd.Value
            let cmdArg = CommandArgs(args, msgEvent, attr)
            if KPX.FsCqHttp.Config.Logging.LogCommandCall then
                x.Logger.Info("Calling handler {0}\r\n Command Context {1}", method.Name, sprintf "%A" msgEvent)
                method.Invoke(x, [| cmdArg |]) |> ignore
