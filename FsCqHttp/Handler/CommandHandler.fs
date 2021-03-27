namespace KPX.FsCqHttp.Handler

open System

open KPX.FsCqHttp.Event
open KPX.FsCqHttp.Handler


[<Sealed>]
[<AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)>]
type CommandHandlerMethodAttribute(command : string, desc, lh) =
    inherit Attribute()

    /// 指令名称
    member x.Command : string = command

    /// 指令概述
    member x.HelpDesc : string = desc

    /// 完整帮助文本
    member x.LongHelp : string = lh

    /// 如果为true则不会被CommandHandlerBase.Commands加载
    member val Disabled = false with get, set

    /// 指示改指令是否在help等指令中隐藏
    member val IsHidden = false with get, set

[<Sealed>]
type CommandEventArgs(cqArg : CqEventArgs, msg : MessageEvent, attr : CommandHandlerMethodAttribute) =
    inherit CqEventArgs(cqArg)

    let rawMsg = msg.Message.ToString()

    let cmdLine =
        rawMsg.Split(
            [| ' '
               KPX.FsCqHttp.Config.Output.TextTable.FullWidthSpace |],
            StringSplitOptions.RemoveEmptyEntries
        )

    /// 原始消息对象
    member x.MessageEvent = msg

    /// 原始消息文本
    member x.RawMessage = rawMsg

    /// 空格拆分后的所有字符串
    member x.CommandLine = cmdLine

    /// 命令名称
    member x.CommandName = attr.Command

    member x.CommandAttrib = attr

    /// 不包含指令的部分
    member val Arguments = cmdLine.[1..]

    /// 从字符串获取可能的指令名
    static member TryGetCommand(str : string) =
        let idx = str.IndexOf(' ')
        if idx = -1 then str else str.[0..idx - 1]

type CommandInfo =
    { CommandAttribute : CommandHandlerMethodAttribute
      MethodName : string
      MethodAction : Action<CommandEventArgs> }

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

                    let d =
                        method.CreateDelegate(typeof<Action<CommandEventArgs>>, x)
                        :?> Action<CommandEventArgs>

                    let cmd =
                        { CommandAttribute = attr
                          MethodName = method.Name
                          MethodAction = d }

                    if not attr.Disabled then commands.Add(cmd)
                    commandGenerated <- true

        commands :> Collections.Generic.IEnumerable<_>
