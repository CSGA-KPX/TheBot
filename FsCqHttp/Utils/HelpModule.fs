﻿module KPX.FsCqHttp.Utils.HelpModule

open System
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Handler.CommandHandlerBase
open KPX.FsCqHttp.Utils.TextTable

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
            let tt = TextTable.FromHeader([|"命令"; "说明"|])
            for (attrib, _) in attribs do
                if not attrib.IsHidden then
                    let cmd = sprintf "%s%s" KPX.FsCqHttp.Config.Command.CommandStart attrib.Command
                    let desc = attrib.HelpDesc
                    tt.AddRow(cmd, desc)
            using (msgArg.OpenResponse(true)) (fun ret -> ret.Write(tt))
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