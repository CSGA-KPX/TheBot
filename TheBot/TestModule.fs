module TestModule
open KPX.FsCqHttp.Handler.CommandHandlerBase

type TestModule() = 
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("test", "显示一条测试信息", "")>]
    member x.HandleTest(msgArg : CommandArgs) =
        msgArg.CqEventArgs.QuickMessageReply("Test success!")