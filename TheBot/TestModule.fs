module TheBot.Module.TestModule

open System
open System.Text
open System.Drawing

open KPX.FsCqHttp.Handler.CommandHandlerBase

open TheBot.Utils.TextTable

type TestModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("tttest", "", "")>]
        member x.HandleTest(msgArg : CommandArgs) =
            ()
            (*
            let api = msgArg.ApiCaller.CallApi<KPX.FsCqHttp.Api.SystemApi.GetGroupList>()

            let tt = AutoTextTable<KPX.FsCqHttp.Api.SystemApi.GroupInfo>([|
                "群号", fun i -> box (i.GroupId)
                "名称", fun i -> box (i.GroupName)
                |])
            for g in api.Groups do 
                tt.AddObject(g)
            msgArg.QuickMessageReply(tt.ToString())
            *)