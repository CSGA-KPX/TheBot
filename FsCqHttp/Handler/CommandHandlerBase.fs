namespace KPX.FsCqHttp.Handler.CommandHandlerBase

open System
open KPX.FsCqHttp.DataType.Event
open KPX.FsCqHttp.Handler

[<AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)>]
type CommandHandlerMethodAttribute(command : string, desc, lh) =
    inherit Attribute()
    member val Command : string = CommandHandlerMethodAttribute.CommandStart + command.ToLowerInvariant()
    member val HelpDesc : string = desc
    member val LongHelp : string = lh

    static member CommandStart = "#"

type CommandArgs(msg : Message.MessageEvent, cqArg : ClientEventArgs) =
    let rawMsg = msg.Message.ToString()
    let cmdLine = rawMsg.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)
    let isCmd = rawMsg.StartsWith(CommandHandlerMethodAttribute.CommandStart)

    member val MessageEvent = msg
    member val CqEventArgs = cqArg
    member val RawMessage = rawMsg
    member val CommandLine = cmdLine

    member val Command : string option = if isCmd then Some(cmdLine.[0].ToLowerInvariant())
                                         else None

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
                let msgArg = CommandArgs(msgEvent, args)
                x.Logger.Info("Calling handler {0}", method.Name)
                try
                    method.Invoke(x, [| msgArg |]) |> ignore
                with
                | :? Reflection.TargetInvocationException as e -> 
                    x.Logger.Fatal("Exception caught: {0}", e.ToString())
                    args.QuickMessageReply(sprintf "发生错误：%s" (e.InnerException.Message))
