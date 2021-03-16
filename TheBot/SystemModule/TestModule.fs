namespace KPX.TheBot.Module.TestModule

open System
open System.Text
open System.Drawing

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextTable

#nowarn "1182"


type TestModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("#test", "", "", IsHidden = true, Disabled = true)>]
    member x.HandleTest(cmdArg : CommandEventArgs) = ()
