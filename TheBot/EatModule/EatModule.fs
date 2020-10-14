module TheBot.Module.EatModule.Instance

open System
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse

open TheBot.Module.EatModule.Utils

open TheBot.Utils.Dicer

type EatModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("eat", "吃什么？", "#eat 晚餐")>]
    member x.HandleEat(msgArg : CommandArgs) =
        let at = msgArg.MessageEvent.Message.GetAts() |> Array.tryHead

        use ret = msgArg.OpenResponse()
        
        match at with
        | Some Message.AtUserType.All -> ret.AbortExecution(InputError, "你要请客吗？")
        | Some (Message.AtUserType.User uid) when uid = msgArg.SelfId || uid = msgArg.MessageEvent.UserId ->
            use s = TheBot.Utils.EmbeddedResource.GetResFileStream("Funny.jpg")
            use img = Drawing.Bitmap.FromStream(s) :?> Drawing.Bitmap
            let msg = Message.Message()
            msg.Add(img)
            msgArg.QuickMessageReply(msg)
            ret.AbortExecution(IgnoreError, "")

        | Some (Message.AtUserType.User uid) ->
            let atUserName = GroupApi.GetGroupMemberInfo(msgArg.MessageEvent.GroupId, uid)
            msgArg.ApiCaller.CallApi(atUserName)
            ret.WriteLine("{0} 为 {1} 投掷：", msgArg.MessageEvent.DisplayName, atUserName.DisplayName)
        | None -> ()

        let seed  = if at.IsSome then 
                        SeedOption.SeedByAtUserDay(msgArg.MessageEvent)
                    else
                        SeedOption.SeedByUserDay(msgArg.MessageEvent)

        let dicer = new Dicer(seed, AutoRefreshSeed = false)

        match msgArg.Arguments.Length with
        | 0 -> ret.AbortExecution(InputError, "自选输菜名，预设套餐：早/中/晚/加/火锅/萨莉亚")
        | 1 -> 
            // 一个选项时处理套餐
            let mutable str = msgArg.Arguments.[0]
            if eatAlias.ContainsKey(str) then str <- eatAlias.[str]
            if eatFuncs.ContainsKey(str) then
                ret.Write(eatFuncs.[str](dicer))
            else
                for l in whenToEat(dicer, msgArg.Arguments) do ret.WriteLine(l)
        | _ ->
            for l in whenToEat(dicer, msgArg.Arguments) do ret.WriteLine(l)
