namespace KPX.FsCqHttp.Handler

open System
open KPX.FsCqHttp.DataType.Event
open KPX.FsCqHttp.Handler

[<AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)>]
type CommandHandlerMethodAttribute(command : string, desc, lh) =
    inherit Attribute()

    /// 指令名称（转化为小写），不含CommandStart字符串
    member x.Command : string = command.ToLowerInvariant()

    /// 指令名称（转化为小写），含CommandStart字符串
    member x.FullCommand = x.CommandStart + x.Command

    /// 指令概述
    member x.HelpDesc : string = desc

    /// 完整帮助文本
    member x.LongHelp : string = lh

    /// 指示改指令是否在help等指令中隐藏
    member val IsHidden = false with get, set

    /// 使用自定义的指令起始符，默认为""，使用配置文件
    member val AltCommandStart = "" with get, set

    /// 获取该指令的指令起始符
    member x.CommandStart = 
        if String.IsNullOrWhiteSpace(x.AltCommandStart) then
            KPX.FsCqHttp.Config.Command.CommandStart
        else
            x.AltCommandStart

// TODO: 处理AltCommandStart
type CommandArgs(cqArg : ClientEventArgs, msg : Message.MessageEvent, attr : CommandHandlerMethodAttribute) =
    inherit ClientEventArgs(cqArg)

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

    let commands = Collections.Generic.List<_>()

    do
        for method in x.GetType().GetMethods() do
            let ret = method.GetCustomAttributes(typeof<CommandHandlerMethodAttribute>, true)
            for attrib in ret do
                let attr = attrib :?> CommandHandlerMethodAttribute
                let cs = attr.CommandStart
                let key = (cs + attr.Command).ToLowerInvariant()
                commands.Add(key, attr, method)

    member x.Commands = commands
