namespace KPX.DicePlugin.LifestyleModule

open KPX.FsCqHttp.Api.Group
open KPX.FsCqHttp.Event
open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Utils.Subcommands
open KPX.FsCqHttp.Utils.TextResponse

open KPX.FsCqHttp.Testing

open KPX.DicePlugin.LifestyleModule.EatUtils

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.Utils.Dicer


type EatSubCommand =
    | [<AltCommandName("早餐", "早饭", "早")>] Breakfast
    | [<AltCommandName("午餐", "中", "中饭", "午")>] Lunch
    | [<AltCommandName("晚餐", "晚餐", "晚")>] Dinner
    | [<AltCommandName("加餐", "夜宵", "加")>] Extra
    | [<AltCommandName("火锅")>] Hotpot
    | [<AltCommandName("饮料")>] Drinks
    | [<AltCommandName("零食")>] Snacks

    interface ISubcommandTemplate with
        member x.Usage =
            match x with
            | Breakfast -> "早餐"
            | Lunch -> "午餐"
            | Dinner -> "晚餐"
            | Extra -> "加餐"
            | Hotpot -> "火锅"
            | Drinks -> "饮料"
            | Snacks -> "零食"

type EatModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethod("#eat",
                           "投掷吃什么",
                           "#eat 食物名称或预设名单
预设名单：早 中 晚 加 火锅
可以@一个群友帮他选。")>]
    member x.HandleEat(cmdArg: CommandEventArgs) =
        use ret = cmdArg.OpenResponse(ForceText)

        if cmdArg.HeaderArgs.Length = 0 then
            let help = SubcommandParser.GenerateHelp<EatSubCommand>()

            for line in help do
                ret.WriteLine(line)
        else
            let mutable seed = SeedOption.SeedByUserDay(cmdArg.MessageEvent)

            let at = cmdArg.MessageEvent.Message.TryGetAt()

            match at with
            | Some AtUserType.All //TryGetAt不接受@all，不会匹配
            | None -> ()
            | Some (AtUserType.User uid) when uid = cmdArg.BotUserId || uid = cmdArg.MessageEvent.UserId ->
                // @自己 @Bot 迷惑行为
                use s = EmbeddedResource.GetResFileStream("DicePlugin.Resources.Funny.jpg")

                use img = SkiaSharp.SKImage.FromEncodedData(s)

                let msg = Message()
                msg.Add(img)
                cmdArg.Reply(msg)
                ret.Abort(IgnoreError, "")

            | Some (AtUserType.User uid) ->
                seed <- SeedOption.SeedByAtUserDay(cmdArg.MessageEvent)
                // 私聊不会有at，所以肯定是群聊消息
                let gEvent = cmdArg.MessageEvent.AsGroup()

                let atUserInfo = GetGroupMemberInfo(gEvent.GroupId, uid) |> cmdArg.ApiCaller.CallApi

                ret.WriteLine("{0} 为 {1} 投掷：", cmdArg.MessageEvent.DisplayName, atUserInfo.DisplayName)

            let dicer = Dicer(seed)
            dicer.Freeze()

            match SubcommandParser.Parse<EatSubCommand>(cmdArg) with
            | None -> scoreByMeals dicer cmdArg.HeaderArgs ret
            | Some Breakfast -> mealsFunc "早餐" breakfast dicer ret
            | Some Lunch -> mealsFunc "午餐" dinner dicer ret
            | Some Dinner -> mealsFunc "晚餐" dinner dicer ret
            | Some Extra -> mealsFunc "加餐" breakfast dicer ret
            | Some Hotpot -> hotpotFunc dicer ret
            | Some Drinks -> mealsFunc "饮料" breakfast dicer ret
            | Some Snacks -> mealsFunc "零食" breakfast dicer ret

    [<CommandHandlerMethod("#饮料", "投掷喝什么饮料，可以@一个群友帮他选", "")>]
    member x.HandleDrink(cmdArg: CommandEventArgs) =
        let msg = cmdArg.MessageEvent.Message
        let tmp = Message()
        tmp.Add("#eat 饮料")

        for at in msg.GetAts() do
            tmp.Add(at)

        let api = KPX.FsCqHttp.Api.Context.RewriteCommand(cmdArg, Seq.singleton<ReadOnlyMessage> tmp)

        cmdArg.ApiCaller.CallApi(api) |> ignore

    [<TestFixture>]
    member x.TestEat() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#eat")
        tc.ShouldNotThrow("#eat AAA")
        tc.ShouldNotThrow("#eat 早")
        tc.ShouldNotThrow("#eat 中")
        tc.ShouldNotThrow("#eat 晚")
        tc.ShouldNotThrow("#饮料")
