namespace KPX.FsCqHttp.Handler

open System

open System.Reflection
open KPX.FsCqHttp
open KPX.FsCqHttp.Handler


[<Sealed>]
[<AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)>]
type CommandHandlerMethodAttribute(command: string, desc, lh) =
    inherit Attribute()

    /// 指令名称
    member x.Command: string = command

    /// 指令概述
    member x.HelpDesc: string = desc

    /// 完整帮助文本
    member x.LongHelp: string = lh

    /// 如果为true则不会被CommandHandlerBase.Commands加载
    member val Disabled = false with get, set

    /// 指示改指令是否在help等指令中隐藏
    member val IsHidden = false with get, set

    /// 只是该指令是否无视调度器，尽快执行
    /// 适用于极端条件下的指令
    member val ExecuteImmediately = false with get, set

[<Sealed>]
type CommandEventArgs internal (args: CqMessageEventArgs, attr: CommandHandlerMethodAttribute) =
    inherit CqMessageEventArgs(args.ApiCaller, args.RawEvent, args.Event)

    let lines =
        [| let rawMsg = args.Event.Message.ToString()
           use sr = new IO.StringReader(rawMsg)

           // 把指令文本抠掉，这样就只剩下参数了
           for _ = 0 to attr.Command.Length - 1 do
               sr.Read() |> ignore

           let mutable hasMore = true

           while hasMore do
               let line = sr.ReadLine()

               if isNull line then
                   // 确保一定有一行
                   yield String.Empty
                   hasMore <- false
               else if not <| String.IsNullOrWhiteSpace(line) then
                   yield line |]

    /// 指令第一行
    member x.HeaderLine = lines |> Seq.head

    /// 除第一行以外的信息
    member x.BodyLines = lines |> Seq.tail

    /// 所有行
    member x.AllLines = lines

    /// 第一行按参数拆分
    member x.HeaderArgs = x.HeaderLine |> CommandEventArgs.SplitArguments

    /// 后续行按参数拆分
    member x.BodyArgs = x.BodyLines |> Seq.map CommandEventArgs.SplitArguments

    /// 所有行按参数拆分
    member x.AllArgs = lines |> Seq.map CommandEventArgs.SplitArguments

    /// 原始消息对象
    member x.MessageEvent = args.Event

    member x.CommandAttrib = attr

    /// 命令名称
    member x.CommandName = attr.Command

    /// 从字符串获取可能的指令名
    static member TryGetCommand(str: string) =
        let idx = str.IndexOfAny([| ' '; '\r'; '\n' |])

        if idx = -1 then
            str
        else
            str.[0..idx - 1]

    static member SplitArguments(str : string) =
        str.Split([| ' '; Config.FullWidthSpace |], StringSplitOptions.RemoveEmptyEntries)

type ICommandResponse =
    abstract Response: CqMessageEventArgs -> unit

type MethodAction =
    /// 方法返回unit，由方法自行处理指令回复
    | ManualAction of Action<CommandEventArgs>
    /// 方法返回ICommandResponse对象，由调度器处理指令回复
    | AutoAction of Func<CommandEventArgs, ICommandResponse>

    static member CreateFrom(method: MethodInfo, instance: obj) =
        let isAuto = typeof<ICommandResponse>.IsAssignableFrom (method.ReturnType)

        if isAuto then
            method.CreateDelegate(typeof<Func<CommandEventArgs, ICommandResponse>>, instance)
            :?> Func<CommandEventArgs, ICommandResponse>
            |> AutoAction
        else
            method.CreateDelegate(typeof<Action<CommandEventArgs>>, instance) :?> Action<CommandEventArgs>
            |> ManualAction

type CommandInfo =
    { CommandAttribute: CommandHandlerMethodAttribute
      MethodName: string
      MethodAction: MethodAction }

[<AbstractClass>]
type CommandHandlerBase() =
    inherit HandlerModuleBase()

    let mutable commandGenerated = false

    let commands = ResizeArray<CommandInfo>()

    /// 返回该类中定义的指令
    member x.Commands =
        if not commandGenerated then
            for method in x.GetType().GetMethods() do
                let ret = method.GetCustomAttributes(typeof<CommandHandlerMethodAttribute>, true)

                for attrib in ret do
                    let attr = attrib :?> CommandHandlerMethodAttribute

                    let cmd =
                        { CommandAttribute = attr
                          MethodName = method.Name
                          MethodAction = MethodAction.CreateFrom(method, x) }

                    if not attr.Disabled then
                        commands.Add(cmd)

                    commandGenerated <- true

        commands :> Collections.Generic.IEnumerable<_>
