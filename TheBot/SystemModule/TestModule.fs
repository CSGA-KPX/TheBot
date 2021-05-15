namespace KPX.TheBot.Module.TestModule

open KPX.FsCqHttp.Handler

#nowarn "1182"


type TestModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("#test", "", "", IsHidden = true, Disabled = true)>]
    member x.HandleTest(cmdArg : CommandEventArgs) = ()