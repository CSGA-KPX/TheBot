namespace KPX.FsCqHttp.Handler

open System

open KPX.FsCqHttp
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
type CommandEventArgs(args : CqMessageEventArgs, attr : CommandHandlerMethodAttribute) =
    inherit CqMessageEventArgs(args.ApiCaller, args.RawEvent, args.Event)

    let splitString (str : string) =
        str.Split(
            [| ' '
               Config.FullWidthSpace |],
            StringSplitOptions.RemoveEmptyEntries
        )

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

    member x.HeaderArgs = x.HeaderLine |> splitString

    member x.BodyArgs = x.BodyLines |> Seq.map splitString

    member x.AllArgs = lines |> Seq.map splitString

    /// 原始消息对象
    member x.MessageEvent = args.Event

    member x.CommandAttrib = attr

    /// 命令名称
    member x.CommandName = attr.Command

    /// 从字符串获取可能的指令名
    static member TryGetCommand(str : string) =
        let idx = str.IndexOfAny([| ' '; '\r'; '\n' |])
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
