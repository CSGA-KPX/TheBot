namespace KPX.TheBot.Module.DiceModule.DiceModule

open System

open KPX.FsCqHttp.Message

open KPX.FsCqHttp.Api.System
open KPX.FsCqHttp.Api.Group

open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

open KPX.TheBot.Utils.Dicer

open KPX.TheBot.Module.DiceModule.Utils


type DiceModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("cc", "同#c，但每次结果随机", "A B C D")>]
    [<CommandHandlerMethodAttribute("c", "对多个选项1d100", "A B C D")>]
    member x.HandleChoices(cmdArg : CommandEventArgs) =
        let atUser = cmdArg.MessageEvent.Message.TryGetAt()
        use sw = cmdArg.OpenResponse(ForceText)

        if atUser.IsSome then
            let loginInfo = cmdArg.ApiCaller.CallApi<GetLoginInfo>()

            match atUser.Value with
            | AtUserType.All -> sw.AbortExecution(InputError, "公共事件请at bot账号")
            | AtUserType.User x when x = cmdArg.BotUserId                                              && not
                                                 <| cmdArg.RawMessage.Contains(loginInfo.Nickname) ->
                sw.WriteLine("公投：")
            | AtUserType.User x ->
                let atUserInfo =
                    GetGroupMemberInfo(cmdArg.MessageEvent.GroupId, x)
                    |> cmdArg.ApiCaller.CallApi

                sw.WriteLine("{0} 为 {1} 投掷：", cmdArg.MessageEvent.DisplayName, atUserInfo.DisplayName)

        let tt = TextTable("1D100", "选项")

        [| for arg in cmdArg.Arguments do
            let m = ChoiceHelper.YesOrNoRegex.Match(arg)

            if m.Success then
                yield
                    m.Groups.[1].Value
                    + m.Groups.[2].Value
                    + m.Groups.[4].Value

                yield
                    m.Groups.[1].Value
                    + m.Groups.[3].Value
                    + m.Groups.[4].Value
            else
                yield arg |]
        |> Array.map
            (fun c ->
                let seed =
                    if cmdArg.CommandName = "cc" then Array.singleton SeedOption.SeedRandom
                    else if atUser.IsSome then SeedOption.SeedByAtUserDay(cmdArg.MessageEvent)
                    else SeedOption.SeedByUserDay(cmdArg.MessageEvent)

                let dicer = Dicer(seed).Freeze()
                (c, dicer.GetRandom(100u, c)))
        |> Array.sortBy snd
        |> Array.iter (fun (c, n) -> tt.AddRow((sprintf "%03i" n), c))

        sw.Write(tt)

    [<CommandHandlerMethodAttribute("jrrp", "（兼容）今日人品值", "", AltCommandStart = ".")>]
    [<CommandHandlerMethodAttribute("jrrp", "今日人品值", "")>]
    member x.HandleJrrp(cmdArg : CommandEventArgs) =
        let dicer =
            Dicer(SeedOption.SeedByUserDay(cmdArg.MessageEvent))

        let jrrp = dicer.GetRandom(100u)
        cmdArg.QuickMessageReply(sprintf "%s今日人品值是：%i" cmdArg.MessageEvent.DisplayName jrrp)

    [<CommandHandlerMethodAttribute("cal", "计算器", "")>]
    member x.HandleCalculator(cmdArg : CommandEventArgs) =
        let sb = Text.StringBuilder()
        let parser = DiceExpression.DiceExpression()
        let arg = String.Join(" ", cmdArg.Arguments)
        let ret = parser.TryEval(arg)

        match ret with
        | Error e ->
            sb.Append(sprintf "对%s求值失败：%s" arg e.Message)
            |> ignore
        | Ok i ->
            let sum =
                String.Format("{0:N2}", i.Sum).Replace(".00", "")

            sb.AppendFormat("{0} = {1}", arg, sum) |> ignore

        cmdArg.QuickMessageReply(sb.ToString())

    [<CommandHandlerMethodAttribute("选择题", "考试专用", "")>]
    member x.HandleChoice(cmdArg : CommandEventArgs) =
        let mutable count = 10

        for arg in cmdArg.Arguments do
            let (succ, i) = UInt32.TryParse(arg)

            if succ then
                let i = int (i)

                if i > 100 then cmdArg.AbortExecution(InputError, "最多100道") else count <- i

        let choices = [| "A"; "B"; "C"; "D" |]

        let chunks =
            Dicer.RandomDicer.GetRandomArray(choices.Length |> uint32, count)
            |> Array.map (fun x -> choices.[x - 1])
            |> Array.chunkBySize 5 // 5个一组
            |> Array.map (fun chk -> String.Join("", chk))
            |> Array.chunkBySize 4 // 4组一行
            |> Array.map (fun chk -> String.Join(" ", chk))

        cmdArg.QuickMessageReply(String.Join("\r\n", chunks))

    [<CommandHandlerMethodAttribute("gacha", "抽10连 概率3%", "")>]
    member x.HandleGacha(cmdArg : CommandEventArgs) =
        let mutable cutoff = 3
        let mutable count = 10

        for arg in cmdArg.Arguments do
            let (succ, i) = UInt32.TryParse(arg)

            if succ then
                let i = int (i)

                if i > 300 then cmdArg.AbortExecution(InputError, "一井还不够你用？")
                elif i >= 10 then count <- i
                else cutoff <- i

        let ret =
            Array.init count (fun _ -> Dicer.RandomDicer.GetRandom(100u))
            |> Array.countBy (fun x -> if x <= cutoff then "红" else "黑")
            |> Array.sortBy (fst)
            |> Array.map (fun (s, c) -> sprintf "%s(%i)" s c)

        let reply =
            Text
                .StringBuilder()
                .AppendLine(sprintf "概率%i%% 抽数%i" cutoff count)
                .Append(String.Join(" ", ret))
                .ToString()

        cmdArg.QuickMessageReply(reply)
