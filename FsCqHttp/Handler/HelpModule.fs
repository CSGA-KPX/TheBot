module KPX.FsCqHttp.Handler.HelpModule

open System
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Handler.CommandHandlerBase

[<Literal>]
let private helpHelp = "不加选项用来查看所有命令，加命令名查看命令帮助
如#help help"

type HelpModule() =
    inherit CommandHandlerBase()

    static let attribs =
        [| 
            for t in HandlerModuleBase.AllDefinedModules do
                for method in t.GetMethods() do
                    let ret = method.GetCustomAttributes(typeof<CommandHandlerMethodAttribute>, true)
                    for attrib in ret do
                        let attrib = attrib :?> CommandHandlerMethodAttribute
                        yield attrib, method
        |]

    [<CommandHandlerMethodAttribute("help", "显示已知命令或显示命令文档", helpHelp)>]
    member x.HandleHelp(msgArg : CommandArgs) =
        if msgArg.Arguments.Length = 0 then
            let sw = new IO.StringWriter()
            for (attrib, _) in attribs do
                if not attrib.IsHidden then
                    sw.WriteLine("{0} {1}", attrib.Command, attrib.HelpDesc)
            msgArg.QuickMessageReply(sw.ToString())
        else
            for arg in msgArg.Arguments do
                let str = KPX.FsCqHttp.Config.Command.CommandStart + arg.ToLowerInvariant()
                let ret =
                    attribs |> Array.tryFind (fun (cmd, _) -> cmd.Command = str || cmd.Command = arg.ToLowerInvariant())
                if ret.IsSome then
                    let (cmd, _) = ret.Value
                    msgArg.QuickMessageReply(cmd.LongHelp)
                else
                    msgArg.QuickMessageReply(sprintf "找不到命令%s" str)
