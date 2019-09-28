module KPX.FsCqHttp.Handler.ModuleManager
open System
open System.Reflection
open KPX.FsCqHttp.DataType.Event
open KPX.FsCqHttp.Handler.Base
open KPX.FsCqHttp.Handler.CommandHandlerBase

let AllDefinedModules = 
    [|
        yield! Reflection.Assembly.GetExecutingAssembly().GetTypes()
        yield! Reflection.Assembly.GetEntryAssembly().GetTypes()
    |]
        
    |> Array.filter(fun t -> 
        t.IsSubclassOf(typeof<HandlerModuleBase>) &&
            (not <| t.IsAbstract))
    |> Array.map (fun t ->
        Activator.CreateInstance(t) :?> HandlerModuleBase)



type HelpModule() = 
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("help", "显示已知命令的文档", "")>]
    member x.HandleHelp(msgArg : CommandArgs) = 
        let attribs = 
            [|
                for t in AllDefinedModules do 
                    let isCommand = 
                        t :? KPX.FsCqHttp.Handler.CommandHandlerBase.CommandHandlerBase
                    if isCommand then
                        let t = t :?> KPX.FsCqHttp.Handler.CommandHandlerBase.CommandHandlerBase
                        yield t.GetType().Name, (t.Commands)
            |]

        let sw = new IO.StringWriter()
        for (m, cmds) in attribs do 
            sw.WriteLine("模块：{0}", m)
            for (attrib,_) in cmds do
                sw.WriteLine("\t {0} {1} : {2}", attrib.Command, attrib.HelpArgs, attrib.HelpDesc)
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())