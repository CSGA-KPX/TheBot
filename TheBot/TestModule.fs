module TestModule
open System
open KPX.FsCqHttp.DataType.Event
open KPX.FsCqHttp.Instance.Base
open CommandHandlerBase

type TestModule() = 
    inherit CommandHandlerBase()

    [<MessageHandlerMethodAttribute("test", "显示一条测试信息", "")>]
    member x.HandleTest(str : string, arg : ClientEventArgs, msg : Message.MessageEvent) =
        arg.QuickMessageReply("Test success!")