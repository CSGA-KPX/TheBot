namespace KPX.DicePlugin.LifestyleModule

open System.Collections.Generic

open KPX.FsCqHttp.Api.Group
open KPX.FsCqHttp.Event
open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Utils.UserOption
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

    interface ISubcommandTemplate with
        member x.Usage =
            match x with
            | Breakfast -> "早餐"
            | Lunch -> "午餐"
            | Dinner -> "晚餐"
            | Extra -> "加餐"
            | Hotpot -> "火锅"

type EatModule() =
    inherit CommandHandlerBase()

    let allKnownOpts =
        seq {
            yield! breakfast
            yield! dinner
            yield! drinks
        }
        |> HashSet<string>

    let newEat = HashSet<string>()
    let newDrink = HashSet<string>()

    let tryAdd (knownSet: HashSet<_>) (newSet: HashSet<_>) (opt) =
        if not <| knownSet.Contains(opt) then
            newSet.Add(opt) |> ignore

    member private _.GetDicer(cmdArg: CommandEventArgs, ret: TextResponse) =
        let mutable seed = DiceSeed.SeedByUserDay(cmdArg.MessageEvent)

        let at = cmdArg.MessageEvent.Message.TryGetAt()

        match at with
        | Some AtUserType.All //TryGetAt不接受@all，不会匹配
        | None -> ()
        | Some(AtUserType.User uid) when uid = cmdArg.BotUserId || uid = cmdArg.MessageEvent.UserId ->
            // @自己 @Bot 迷惑行为
            use s = EmbeddedResource.GetResFileStream("DicePlugin.Resources.Funny.jpg")

            use img = SkiaSharp.SKImage.FromEncodedData(s)

            let msg = Message()
            msg.Add(img)
            cmdArg.Reply(msg)
            cmdArg.Abort(IgnoreError, "")

        | Some(AtUserType.User uid) ->
            seed <- DiceSeed.SeedByAtUserDay(cmdArg.MessageEvent)
            // 私聊不会有at，所以肯定是群聊消息
            let gEvent = cmdArg.MessageEvent.AsGroup()

            let atUserInfo = GetGroupMemberInfo(gEvent.GroupId, uid) |> cmdArg.ApiCaller.CallApi

            ret.WriteLine("{0} 为 {1} 投掷：", cmdArg.MessageEvent.DisplayName, atUserInfo.DisplayName)

        let dicer = Dicer(seed)
        dicer.Freeze()

        dicer

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
            let dicer = x.GetDicer(cmdArg, ret)

            match SubcommandParser.Parse<EatSubCommand>(cmdArg) with
            | None ->
                for opt in cmdArg.HeaderArgs do
                    tryAdd allKnownOpts newEat opt

                scoreByMeals dicer cmdArg.HeaderArgs ret
            | Some Breakfast -> mealsFunc "早餐" breakfast dicer ret
            | Some Lunch -> mealsFunc "午餐" dinner dicer ret
            | Some Dinner -> mealsFunc "晚餐" dinner dicer ret
            | Some Extra -> mealsFunc "加餐" breakfast dicer ret
            | Some Hotpot -> hotpotFunc dicer ret

    [<CommandHandlerMethod("#drink", "投掷喝什么饮料，可以@一个群友帮他选", "")>]
    member x.HandleDrink(cmdArg: CommandEventArgs) =
        use ret = cmdArg.OpenResponse(ForceText)
        let dicer = x.GetDicer(cmdArg, ret)
        let options = cmdArg.HeaderArgs

        match options.Length with
        | 0 -> mealsFunc "饮料" drinks dicer ret
        | _ ->
            for opt in cmdArg.HeaderArgs do
                tryAdd allKnownOpts newDrink opt

            let mapped =
                EatChoices(options, dicer, "喝").MappedOptions
                |> Seq.sortBy (fun opt -> opt.Value)
                |> Seq.map (fun opt -> $"%s{opt.Original}(%s{opt.DescribeValue()})")

            ret.WriteLine(sprintf "%s" (System.String.Join(" ", mapped)))

    [<CommandHandlerMethod("#showneweats", "显示新添加的eat选项", "")>]
    member x.ShowNewOpts(cmdArg: CommandEventArgs) =
        use ret = cmdArg.OpenResponse(ForceText)
        ret.WriteLine("吃的：")
        ret.WriteLine(System.String.Join(" ", newEat))
        ret.WriteLine("饮料：")
        ret.WriteLine(System.String.Join(" ", newDrink))

    [<TestFixture>]
    member x.TestEat() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#eat")
        tc.ShouldNotThrow("#eat AAA")
        tc.ShouldNotThrow("#eat 早")
        tc.ShouldNotThrow("#eat 中")
        tc.ShouldNotThrow("#eat 晚")
