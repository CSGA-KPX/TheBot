namespace KPX.FsCqHttp.Handler.CommandHandlerBase

open System
open KPX.FsCqHttp.DataType.Event
open KPX.FsCqHttp.Handler

[<AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)>]
type CommandHandlerMethodAttribute(command : string, desc, lh) =
    inherit Attribute()
    member val Command : string = KPX.FsCqHttp.Config.Command.CommandStart + command.ToLowerInvariant()
    member x.HelpDesc : string = desc
    member x.LongHelp : string = lh

type CommandArgs(msg : Message.MessageEvent, cqArg : ClientEventArgs) =
    inherit ClientEventArgs(cqArg.ApiCaller, cqArg.RawEvent)

    let rawMsg = msg.Message.ToString()
    let cmdLine = rawMsg.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)
    let isCmd = rawMsg.StartsWith(KPX.FsCqHttp.Config.Command.CommandStart)

    member x.MessageEvent = msg
    member x.RawMessage = rawMsg
    member x.CommandLine = cmdLine

    /// 指令，含有'CommandStart'
    member val Command : string option = if isCmd then
                                            Some(cmdLine.[0].ToLowerInvariant())
                                         else None

    member x.IsCommand(str : string) = x.Command.Value = (KPX.FsCqHttp.Config.Command.CommandStart + str.ToLowerInvariant())

    member val Arguments = [| if isCmd then yield! cmdLine.[1..] |]

[<AbstractClass>]
type CommandHandlerBase(shared : bool) as x =
    inherit HandlerModuleBase(shared)

    new () = CommandHandlerBase(true)

    member val Commands = [| for method in x.GetType().GetMethods() do
                                 let ret = method.GetCustomAttributes(typeof<CommandHandlerMethodAttribute>, true)
                                 for attrib in ret do
                                     let attrib = attrib :?> CommandHandlerMethodAttribute
                                     yield attrib, method |]

    override x.HandleMessage(args, msgEvent) =
        let msgArg = CommandArgs(msgEvent, args)
        if msgArg.Command.IsSome then
            let matched = x.Commands |> Array.filter (fun (a, _) -> msgArg.Command.Value = a.Command)
            for (_, method) in matched do
                if KPX.FsCqHttp.Config.Logging.LogCommandCall then
                    x.Logger.Info("Calling handler {0}\r\n Command Context {1}", method.Name, sprintf "%A" msgEvent)
                try
                    method.Invoke(x, [| msgArg |]) |> ignore
                with
                | :? Reflection.TargetInvocationException as e -> 
                    x.Logger.Fatal("Exception caught: {0}", e.ToString())
                    args.QuickMessageReply(sprintf "发生错误：%s" (e.InnerException.Message))
