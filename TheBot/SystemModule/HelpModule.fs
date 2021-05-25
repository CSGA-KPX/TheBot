namespace KPX.TheBot.Module.HelpModule

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Testing

open KPX.FsCqHttp.Utils.HelpModule


type HelpModule() =
    inherit HelpModuleBase()

    [<CommandHandlerMethod("#help",
                                    "显示已知命令或显示命令文档详见#help #help",
                                    "没有参数显示已有指令。加指令名查看指令帮助如#help #help")>]
    [<CommandHandlerMethod(".help", "同#help", "")>]
    member x.HandleHelp(cmdArg : CommandEventArgs) = x.HelpCommandImpl(cmdArg)

    [<TestFixture>]
    member x.TestHelp() = 
        let tc = TestContext(x)
        tc.ShouldNotThrow("#help")
        tc.ShouldNotThrow("#help #help")
        tc.ShouldNotThrow("#help .help")
        tc.ShouldNotThrow(".help")
        tc.ShouldNotThrow(".help .help")
        tc.ShouldNotThrow(".help #help")
