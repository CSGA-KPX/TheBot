module CommandHandlerBase
open System
open System.Reflection
open KPX.FsCqHttp.DataType.Event
open KPX.FsCqHttp.Instance.Base

(*
TODO:
    把属性定义改为Class
    每个命令都定义在Class中
    又一个单独的HelpTextClass用反射从Assembly中拉数据帮助文本

*)

[<AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)>]
type MessageHandlerMethodAttribute(command : string, desc, args) = 
    inherit Attribute()
    member val Command  : string = MessageHandlerMethodAttribute.CommandStart + command.ToLowerInvariant()
    member val HelpDesc : string = desc
    member val HelpArgs : string = args

    static member CommandStart = "#"

type CommandArgs(msg : Message.MessageEvent, cqArg : ClientEventArgs) = 
    let rawMsg  = msg.Message.ToString()
    let cmdLine = rawMsg.Split(' ', StringSplitOptions.RemoveEmptyEntries)
    let isCmd   = rawMsg.StartsWith(MessageHandlerMethodAttribute.CommandStart)

    member val MessageEvent = msg
    member val CqEventArgs  = cqArg
    member val RawMessage = rawMsg
    member val CommandLine = cmdLine
    member val Command : string option =    
        if isCmd then
            Some(cmdLine.[0].ToLowerInvariant())
        else
            None

    member val Arguments    = 
        [|
            if isCmd then
                yield! cmdLine.[1..]
        |]

[<AbstractClass>]
type CommandHandlerBase() as x = 
    inherit HandlerModuleBase()

    static let allDefinedModules =
        Reflection.Assembly.GetExecutingAssembly().GetTypes()
        |> Array.filter(fun t -> 
            t.IsSubclassOf(typeof<CommandHandlerBase>) &&
                (not <| t.IsAbstract))
        |> Array.map (fun t ->
            Activator.CreateInstance(t) :?> CommandHandlerBase)

    static member AllDefinedModules = allDefinedModules

    member val Commands =
        [|
            for method in x.GetType().GetMethods() do 
                let ret = method.GetCustomAttributes(typeof<MessageHandlerMethodAttribute>, true)
                for attrib in ret do 
                    let attrib = attrib :?> MessageHandlerMethodAttribute
                    yield attrib, method
        |]

    override x.HandleMessage(args, msgEvent) = 
        let msgArg = new CommandArgs(msgEvent, args)
        if msgArg.Command.IsSome then
            let matched = 
                x.Commands
                |> Array.filter (fun (a, _) ->
                    msgArg.Command.Value = a.Command
                )
            for (_, method) in matched do 
                let msgArg = new CommandArgs(msgEvent, args)
                x.Logger.Info("Calling handler {0}", method.Name)
                method.Invoke(x, [|msgArg|]) |> ignore

type HelpModule() = 
    inherit CommandHandlerBase()

    [<MessageHandlerMethodAttribute("help", "显示已知命令的文档", "")>]
    member x.HandleHelp(msgArg : CommandArgs) = 
        let attribs = 
            [|
                for t in CommandHandlerBase.AllDefinedModules do 
                    yield t.GetType().Name, (t.Commands)
            |]

        let sw = new IO.StringWriter()
        for (m, cmds) in attribs do 
            sw.WriteLine("模块：{0}", m)
            for (attrib,_) in cmds do
                sw.WriteLine("\t {0} {1} : {2}", attrib.Command, attrib.HelpArgs, attrib.HelpDesc)
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())