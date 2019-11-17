﻿module KPX.FsCqHttp.Handler.ModuleManager
open System
open System.Reflection
open KPX.FsCqHttp.DataType.Event
open KPX.FsCqHttp.Handler.Base
open KPX.FsCqHttp.Handler.CommandHandlerBase

let AllDefinedModules = 
    [|
        yield! Assembly.GetExecutingAssembly().GetTypes()
        yield! Assembly.GetEntryAssembly().GetTypes()
    |]
        
    |> Array.filter(fun t -> 
        t.IsSubclassOf(typeof<HandlerModuleBase>) &&
            (not <| t.IsAbstract))
    |> Array.map (fun t ->
        Activator.CreateInstance(t) :?> HandlerModuleBase)


[<Literal>]
let helpHelp = "不加选项用来查看所有命令，加命令名查看命令帮助
如#help help"

type HelpModule() = 
    inherit CommandHandlerBase()

    static let attribs = 
        [|
            for t in AllDefinedModules do 
                let isCommand = 
                    t :? CommandHandlerBase
                if isCommand then
                    let t = t :?> CommandHandlerBase
                    yield! t.Commands
        |]

    [<CommandHandlerMethodAttribute("help", "显示已知命令或显示命令文档", helpHelp)>]
    member x.HandleHelp(msgArg : CommandArgs) = 
        if msgArg.Arguments.Length = 0 then
            let sw = new IO.StringWriter()
            for (attrib, _) in attribs do 
                sw.WriteLine("\t {0} {1}", attrib.Command, attrib.HelpDesc)
            msgArg.CqEventArgs.QuickMessageReply(sw.ToString())
        else
            for arg in msgArg.Arguments do 
                let str = CommandHandlerMethodAttribute.CommandStart + arg.ToLowerInvariant()
                let ret = attribs |> Array.tryFind (fun (cmd, _) -> cmd.Command = str || cmd.Command = arg.ToLowerInvariant())
                if ret.IsSome then
                    let (cmd, _) = ret.Value
                    msgArg.CqEventArgs.QuickMessageReply(cmd.LongHelp)
                else
                    msgArg.CqEventArgs.QuickMessageReply(sprintf "找不到命令%s" str)