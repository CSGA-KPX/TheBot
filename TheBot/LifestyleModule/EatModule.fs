namespace KPX.TheBot.Module.LifestyleModule

open System
open KPX.FsCqHttp.Api.Group
open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse

open KPX.TheBot.Module.LifestyleModule.EatUtils

open KPX.TheBot.Utils.Dicer


type EatModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("eat", "吃什么？", "#eat 晚餐")>]
    [<CommandHandlerMethodAttribute("买菜", "", "")>]
    //[<CommandHandlerMethodAttribute("零食", "", "")>]
    [<CommandHandlerMethodAttribute("饮料", "", "")>]
    member x.HandleEat(cmdArg : CommandEventArgs) =
        let at = cmdArg.MessageEvent.Message.TryGetAt()
        use ret = cmdArg.OpenResponse()

        let mutable seed =
            SeedOption.SeedByUserDay(cmdArg.MessageEvent)

        match at with
        | Some AtUserType.All //TryGetAt不接受@all，不会匹配
        | None -> ()
        | Some (AtUserType.User uid) when uid = cmdArg.BotUserId
                                          || uid = cmdArg.MessageEvent.UserId ->
            // @自己 @Bot 迷惑行为
            use s =
                KPX.TheBot.Utils.EmbeddedResource.GetResFileStream("Funny.jpg")

            use img =
                Drawing.Bitmap.FromStream(s) :?> Drawing.Bitmap

            let msg = Message()
            msg.Add(img)
            cmdArg.QuickMessageReply(msg)
            ret.AbortExecution(IgnoreError, "")

        | Some (AtUserType.User uid) ->
            seed <- SeedOption.SeedByAtUserDay(cmdArg.MessageEvent)

            let atUserInfo =
                GetGroupMemberInfo(cmdArg.MessageEvent.GroupId, uid)
                |> cmdArg.ApiCaller.CallApi

            ret.WriteLine("{0} 为 {1} 投掷：", cmdArg.MessageEvent.DisplayName, atUserInfo.DisplayName)

        let dicer = Dicer(seed).Freeze()

        match cmdArg.Arguments.Length with
        | _ when eatFuncs.ContainsKey(cmdArg.CommandAttrib.Command) ->
            let func = eatFuncs.[cmdArg.CommandAttrib.Command]
            func dicer ret
        | 0 -> ret.AbortExecution(InputError, "自选输菜名，预设套餐：早/中/晚/加/火锅/萨莉亚")
        | 1 when eatAlias.ContainsKey(cmdArg.Arguments.[0]) ->
            let key = eatAlias.[cmdArg.Arguments.[0]]
            let func = eatFuncs.[key]
            func dicer ret
        | _ -> scoreByMeals dicer cmdArg.Arguments ret