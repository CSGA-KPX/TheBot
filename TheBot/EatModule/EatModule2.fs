namespace KPX.TheBot.Module.EatModule.Instance

open System
open KPX.FsCqHttp.Api.Group
open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse

open KPX.TheBot.Module.EatModule.Utils

open KPX.TheBot.Utils.Dicer


type EatModule2() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("eat", "吃什么？", "#eat 晚餐")>]
    member x.HandleEat(cmdArg : CommandEventArgs) =
        let at = cmdArg.MessageEvent.Message.TryGetAt()
        use ret = cmdArg.OpenResponse()
        let mutable seed = SeedOption.SeedByUserDay(cmdArg.MessageEvent)

        match at with
        | Some Sections.AtUserType.All //TryGetAt不接受@all，不会匹配
        | None -> ()
        | Some (Sections.AtUserType.User uid) when uid = cmdArg.BotUserId
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

        | Some (Sections.AtUserType.User uid) ->
            seed <- SeedOption.SeedByAtUserDay(cmdArg.MessageEvent)

            let atUserInfo =
                GetGroupMemberInfo(cmdArg.MessageEvent.GroupId, uid)
                |> cmdArg.ApiCaller.CallApi

            ret.WriteLine("{0} 为 {1} 投掷：", cmdArg.MessageEvent.DisplayName, atUserInfo.DisplayName)
        
        let dicer = Dicer(seed).Freeze()

        match cmdArg.Arguments.Length with
        | 0 -> ret.AbortExecution(InputError, "自选输菜名，预设套餐：早/中/晚/加/火锅/萨莉亚")
        | 1 ->
            // 一个选项时处理套餐
            let mutable str = cmdArg.Arguments.[0]
            if eatAlias.ContainsKey(str) then str <- eatAlias.[str]

            if eatFuncs.ContainsKey(str) then
                ret.Write(eatFuncs.[str] (dicer))
            else
                for l in whenToEat (dicer, cmdArg.Arguments) do
                    ret.WriteLine(l)
        | _ ->
            for l in whenToEat (dicer, cmdArg.Arguments) do
                ret.WriteLine(l)
