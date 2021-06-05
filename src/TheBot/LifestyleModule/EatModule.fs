﻿namespace KPX.TheBot.Module.LifestyleModule

open System
open KPX.FsCqHttp.Api.Group
open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse

open KPX.FsCqHttp.Testing

open KPX.TheBot.Module.LifestyleModule.EatUtils

open KPX.TheBot.Utils.Dicer


type EatModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethod("#eat",
                                    "投掷吃什么",
                                    "#eat 食物名称或预设名单
预设名单：早 中 晚 加 火锅 萨莉亚
可以@一个群友帮他选。")>]
    //[<CommandHandlerMethodAttribute("零食", "", "")>]
    [<CommandHandlerMethod("#饮料", "投掷喝什么饮料，可以@一个群友帮他选", "")>]
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
            cmdArg.Reply(msg)
            ret.Abort(IgnoreError, "")

        | Some (AtUserType.User uid) ->
            seed <- SeedOption.SeedByAtUserDay(cmdArg.MessageEvent)

            let atUserInfo =
                GetGroupMemberInfo(cmdArg.MessageEvent.GroupId, uid)
                |> cmdArg.ApiCaller.CallApi

            ret.WriteLine("{0} 为 {1} 投掷：", cmdArg.MessageEvent.DisplayName, atUserInfo.DisplayName)

        let dicer = Dicer(seed)
        dicer.Freeze()

        match cmdArg.HeaderArgs.Length with
        | _ when eatFuncs.ContainsKey(cmdArg.CommandAttrib.Command) ->
            let func = eatFuncs.[cmdArg.CommandAttrib.Command]
            func dicer ret
        | 0 -> ret.Abort(InputError, "自选输菜名，预设套餐：早/中/晚/加/火锅/萨莉亚")
        | 1 when eatAlias.ContainsKey(cmdArg.HeaderArgs.[0]) ->
            let key = eatAlias.[cmdArg.HeaderArgs.[0]]
            let func = eatFuncs.[key]
            func dicer ret
        | _ -> scoreByMeals dicer cmdArg.HeaderArgs ret

    [<TestFixture>]
    member x.TestEat() = 
        let tc = TestContext(x)
        tc.ShouldThrow("#eat")
        tc.ShouldNotThrow("#eat AAA")
        tc.ShouldNotThrow("#eat 早")
        tc.ShouldNotThrow("#eat 中")
        tc.ShouldNotThrow("#eat 晚")
        tc.ShouldNotThrow("#饮料")