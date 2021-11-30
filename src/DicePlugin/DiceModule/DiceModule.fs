namespace KPX.DicePlugin.DiceModule

open System

open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Event
open KPX.FsCqHttp.Api.System
open KPX.FsCqHttp.Api.Group
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse

open KPX.TheBot.Host.Utils.Dicer
open KPX.DicePlugin.DiceModule.Utils


type DiceModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethod(".c", "同#c，但每次结果随机", "")>]
    [<CommandHandlerMethod("#c",
                           "对多个选项1d100",
                           "#c 选项1 选项2 选项3
可以使用X不X类的短语。如'#c 能不能吃肉'等同于'#c 能吃肉 不能吃肉'
可以@一个群友帮他选")>]
    member x.HandleChoices(cmdArg: CommandEventArgs) =
        let atUser = cmdArg.MessageEvent.Message.TryGetAt()
        use ret = cmdArg.OpenResponse(ForceText)

        if atUser.IsSome then
            let loginInfo = cmdArg.ApiCaller.CallApi<GetLoginInfo>()

            match atUser.Value with
            | AtUserType.All -> ret.Abort(InputError, "公共事件请at bot账号")
            | AtUserType.User x when
                // TODO: 应该使用AllLines轮询
                x = cmdArg.BotUserId && not <| cmdArg.HeaderLine.Contains(loginInfo.Nickname)
                ->
                ret.WriteLine("公投：")
            | AtUserType.User x ->
                let atUserInfo =
                    let gEvent = cmdArg.MessageEvent.AsGroup()

                    GetGroupMemberInfo(gEvent.GroupId, x) |> cmdArg.ApiCaller.CallApi

                ret.WriteLine("{0} 为 {1} 投掷：", cmdArg.MessageEvent.DisplayName, atUserInfo.DisplayName)

        if cmdArg.HeaderArgs.Length = 0 then
            cmdArg.Abort(InputError, "没有选项")

        ret.Table {
            let seed =
                if cmdArg.CommandAttrib.Command = ".c" then
                    Array.singleton SeedOption.SeedRandom
                else if atUser.IsSome then
                    SeedOption.SeedByAtUserDay(cmdArg.MessageEvent)
                else
                    SeedOption.SeedByUserDay(cmdArg.MessageEvent)

            let dicer = Dicer(seed)
            do dicer.Freeze()

            [ CellBuilder() { literal "D100" }; CellBuilder() { literal "选项" } ]

            cmdArg.HeaderArgs
            |> Array.collect ChoiceHelper.expandYesOrNo
            |> Array.map (fun c -> (c, dicer.GetPositive(100u, c)))
            |> Array.sortBy snd
            |> Array.map (fun (c, n) -> [ CellBuilder() { literal $"%03i{n}" }; CellBuilder() { literal c } ])
        }
        |> ignore

    [<CommandHandlerMethod(".jrrp", "（兼容）今日人品值", "")>]
    [<CommandHandlerMethod("#jrrp", "今日人品值", "")>]
    member x.HandleJrrp(cmdArg: CommandEventArgs) =
        let dicer = Dicer(SeedOption.SeedByUserDay(cmdArg.MessageEvent))

        let jrrp = dicer.GetPositive(100u)
        cmdArg.Reply $"%s{cmdArg.MessageEvent.DisplayName}今日人品值是：%i{jrrp}"

    [<CommandHandlerMethod("#cal",
                           "计算器",
                           "支持加减乘除操作和DK操作符，可以测试骰子表达式。
如#c (1D2+5D5K3)/2*3D6")>]
    member x.HandleCalculator(cmdArg: CommandEventArgs) =
        let sb = Text.StringBuilder()
        let parser = DiceExpression.DiceExpression()
        let arg = String.Join(" ", cmdArg.HeaderArgs)
        let ret = parser.TryEval(arg)

        match ret with
        | Error e -> sb.Append $"对%s{arg}求值失败：%s{e.Message}" |> ignore
        | Ok i ->
            let sum = String.Format("{0:N2}", i.Sum).Replace(".00", "")

            sb.AppendFormat("{0} = {1}", arg, sum) |> ignore

        cmdArg.Reply(sb.ToString())

    [<CommandHandlerMethod("#选择题", "考试专用", "")>]
    member x.HandleChoice(cmdArg: CommandEventArgs) =
        let mutable count = 10

        for arg in cmdArg.HeaderArgs do
            let succ, i = UInt32.TryParse(arg)

            if succ then
                let i = int i

                if i > 100 then
                    cmdArg.Abort(InputError, "最多100道")
                else
                    count <- i

        let choices = [| "A"; "B"; "C"; "D" |]

        let chunks =
            Dicer.RandomDicer.GetNaturalArray(choices.Length - 1, count)
            |> Array.map (fun x -> choices.[x])
            |> Array.chunkBySize 5 // 5个一组
            |> Array.map (fun chk -> String.Join("", chk))
            |> Array.chunkBySize 4 // 4组一行
            |> Array.map (fun chk -> String.Join(" ", chk))

        cmdArg.Reply(String.Join("\r\n", chunks))

    [<CommandHandlerMethod("#gacha",
                           "抽10连 概率3%",
                           "接受数字参数。
小于10的记为概率，大于等于10记为抽数，以最后一次出现的为准。
如#gacha 6 300或#gacha 300 6")>]
    member x.HandleGacha(cmdArg: CommandEventArgs) =
        let mutable cutoff = 3
        let mutable count = 10

        for arg in cmdArg.HeaderArgs do
            let succ, i = UInt32.TryParse(arg)

            if succ then
                let i = int i

                if i > 300 then
                    cmdArg.Abort(InputError, "一井还不够你用？")
                elif i >= 10 then
                    count <- i
                else
                    cutoff <- i

        let ret =
            Array.init count (fun _ -> Dicer.RandomDicer.GetPositive(100u) |> int)
            |> Array.countBy (fun x -> if x <= cutoff then "红" else "黑")
            |> Array.sortBy fst
            |> Array.map (fun (s, c) -> $"%s{s}(%i{c})")

        let reply =
            Text
                .StringBuilder()
                .AppendLine(sprintf "概率%i%% 抽数%i" cutoff count)
                .Append(String.Join(" ", ret))
                .ToString()

        cmdArg.Reply(reply)
