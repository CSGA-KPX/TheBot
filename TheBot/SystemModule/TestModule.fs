namespace KPX.TheBot.Module.TestModule

open System

open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Api.Context

open KPX.FsCqHttp.Utils.TextResponse

open KPX.TheBot.Utils.HandlerUtils

#nowarn "1182"


type TestModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("#test", "", "", IsHidden = true, Disabled = true)>]
    member x.HandleTest(cmdArg : CommandEventArgs) = ()

    [<CommandHandlerMethodAttribute("##cmdtest", "（超管）单元测试", "")>]
    member x.HandleCommandTest(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()

        try
            let mi =
                cmdArg.ApiCaller.CallApi<GetCtxModuleInfo>()

            mi.ModuleInfo.TestCallbacks
            |> Seq.toArray
            |> Array.iter (fun test -> test.Invoke())

            cmdArg.QuickMessageReply("成功完成")
        with e -> 
            using (cmdArg.OpenResponse(PreferImage)) (fun ret -> ret.Write(sprintf "%O" e))