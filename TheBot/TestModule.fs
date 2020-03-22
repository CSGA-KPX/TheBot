module TheBot.Module.TestModule

open System
open System.Text
open KPX.FsCqHttp.Handler.CommandHandlerBase
open TheBot.Utils.TextTable

type TestModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("tttest", "", "")>]
        member x.HandleTest(msgArg : CommandArgs) = 
            let tt = AutoTextTable<int>([|
                "Test", fun i -> box i
                "Test", fun i -> box (i+1)
                "Test", fun i -> box (i+10)
                "Test", fun i -> box (i+100)
                "Test", fun i -> box (i+1000)
                |])
            tt.AddRow(1)
            msgArg.QuickMessageReply(tt.ToString())