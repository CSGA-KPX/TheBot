module TheBot.Module.TestModule

open System
open System.Text
open KPX.FsCqHttp.Handler.CommandHandlerBase

type TestModule() =
    inherit CommandHandlerBase()