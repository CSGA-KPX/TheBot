namespace KPX.FsCqHttp.Handler

open System

open KPX.FsCqHttp.Event
open KPX.FsCqHttp.Handler


[<Sealed>]
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

    /// 如果为true则不会被CommandHandlerBase.Commands加载
    member val Disabled = false with get, set

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

[<Sealed>]
type CommandEventArgs(cqArg : CqEventArgs, msg : MessageEvent, attr : CommandHandlerMethodAttribute) =
    inherit CqEventArgs(cqArg)

    let rawMsg = msg.Message.ToString()

    let cmdLine =
        rawMsg.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)

    let cmdName =
        let cmd = cmdLine.[0].ToLowerInvariant()
        let idx = cmd.IndexOf(attr.CommandStart)
        let len = attr.CommandStart.Length
        cmd.[idx + len..]

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

type CommandInfo =
    { CommandName : string
      OwnerModule : HandlerModuleBase
      CommandAttribute : CommandHandlerMethodAttribute
      Method : Reflection.MethodInfo }

[<AbstractClass>]
type CommandHandlerBase() =
    inherit HandlerModuleBase()

    let mutable commandGenerated = false

    let commands = ResizeArray<CommandInfo>()

    /// 返回该类中定义的指令
    member x.Commands =
        if not commandGenerated then
            for method in x.GetType().GetMethods() do
                let ret =
                    method.GetCustomAttributes(typeof<CommandHandlerMethodAttribute>, true)

                for attrib in ret do
                    let attr = attrib :?> CommandHandlerMethodAttribute

                    let key =
                        (attr.CommandStart + attr.Command)
                            .ToLowerInvariant()

                    let cmd =
                        { CommandName = key
                          CommandAttribute = attr
                          Method = method
                          OwnerModule = x }

                    if not attr.Disabled then
                        commands.Add(cmd)
                    commandGenerated <- true

        commands :> Collections.Generic.IEnumerable<_>
