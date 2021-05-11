namespace KPX.TheBot.Module.HelpModule

open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Utils.HelpModule


type HelpModule() =
    inherit HelpModuleBase()

    [<CommandHandlerMethodAttribute("#help",
                                    "显示已知命令或显示命令文档详见#help #help",
                                    "没有参数显示已有指令。加指令名查看指令帮助如#help #help")>]
    [<CommandHandlerMethodAttribute(".help", "同#help", "")>]
    member x.HandleHelp(cmdArg : CommandEventArgs) = x.HelpCommandImpl(cmdArg)
