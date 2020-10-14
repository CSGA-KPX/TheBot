module TheBot.Module.TestModule

open System
open System.Text
open System.Drawing

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextTable


type TestModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("test", "", "", IsHidden = true)>]
        member x.HandleTest(msgArg : CommandArgs) =
            ()