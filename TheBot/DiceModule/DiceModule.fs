module TheBot.Module.DiceModule.DiceModule

open System
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Handler.CommandHandlerBase

open TheBot.Utils.GenericRPN
open TheBot.Utils.Dicer
open TheBot.Utils.TextTable

open TheBot.Module.DiceModule.Utils

type DiceModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("cc", "同#c，但每次结果随机", "A B C D")>]
    [<CommandHandlerMethodAttribute("c", "对多个选项1d100", "A B C D")>]
    member x.HandleChoices(msgArg : CommandArgs) =
        let atUser = msgArg.MessageEvent.Message.GetAts() |> Array.tryHead
        let sw = new IO.StringWriter()
        if atUser.IsSome then
            let loginInfo = msgArg.ApiCaller.CallApi<SystemApi.GetLoginInfo>()
            match atUser.Value with
            | Message.AtUserType.All ->
                failwithf "公共事件请at bot账号"
            | Message.AtUserType.User x when x = msgArg.SelfId
                                          && not <| msgArg.RawMessage.Contains(loginInfo.Nickname) ->
                sw.WriteLine("公投：")
            | Message.AtUserType.User x ->
                let atUserName = GroupApi.GetGroupMemberInfo(msgArg.MessageEvent.GroupId, x)
                msgArg.ApiCaller.CallApi(atUserName)
                sw.WriteLine("{0} 为 {1} 投掷：", msgArg.MessageEvent.GetNicknameOrCard, atUserName.DisplayName)

        let tt = TextTable.FromHeader([| "1D100"; "选项" |])

        [|
            for arg in msgArg.Arguments do 
                let m = ChoiceHelper.YesOrNoRegex.Match(arg)
                if m.Success then
                    yield m.Groups.[1].Value + m.Groups.[2].Value + m.Groups.[4].Value
                    yield m.Groups.[1].Value + m.Groups.[3].Value + m.Groups.[4].Value
                else
                    yield arg
        |]
        |> Array.map (fun c -> 
            let seed = 
                if msgArg.IsCommand("cc") then Array.singleton SeedOption.SeedRandom 
                else
                    if atUser.IsSome then SeedOption.SeedByAtUserDay(msgArg.MessageEvent)
                    else SeedOption.SeedByUserDay(msgArg.MessageEvent)
            let dicer = Dicer(seed, AutoRefreshSeed = false)
            (c, dicer.GetRandom(100u, c)))
        |> Array.sortBy snd
        |> Array.iter (fun (c, n) -> tt.AddRow((sprintf "%03i" n), c))

        sw.Write(tt.ToString())
        msgArg.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("jrrp", "今日人品值", "")>]
    member x.HandleJrrp(msgArg : CommandArgs) =
        let dicer = Dicer(SeedOption.SeedByUserDay(msgArg.MessageEvent))
        let jrrp = dicer.GetRandom(100u)
        msgArg.QuickMessageReply(sprintf "%s今日人品值是：%i" msgArg.MessageEvent.GetNicknameOrCard jrrp)

    [<CommandHandlerMethodAttribute("cal", "计算器", "")>]
    member x.HandleCalculator(msgArg : CommandArgs) =
        let sb = Text.StringBuilder()
        let parser = DiceExpression.DiceExpression()
        let arg = String.Join(" ", msgArg.Arguments)
        let ret = parser.TryEval(arg)
        match ret with
        | Error e -> sb.Append(sprintf "对%s求值失败：%s" arg e.Message) |> ignore
        | Ok i -> sb.Append(sprintf "%s = %O" arg i) |> ignore
        msgArg.QuickMessageReply(sb.ToString())

    [<CommandHandlerMethodAttribute("gacha", "抽10连 概率3%", "")>]
    member x.HandleGacha(msgArg : CommandArgs) =
        let mutable cutoff = 3
        let mutable count  = 10

        for arg in msgArg.Arguments do 
            let (succ, i) = UInt32.TryParse(arg)
            if succ then
                let i = int(i)
                if i > 300 then
                    failwithf "一井还不够你用？"
                elif i >= 10 then
                    count <- i
                else
                    cutoff <- i
        
        let dicer = Dicer(SeedOption.SeedRandom |> Array.singleton )
        let ret =
            Array.init count (fun _ -> dicer.GetRandom(100u))
            |> Array.countBy (fun x -> if x <= cutoff then "红" else "黑")
            |> Array.sortBy (fst)
            |> Array.map (fun (s, c) -> sprintf "%s(%i)" s c)

        let reply = 
            Text.StringBuilder()
                .AppendLine(sprintf "概率%i%% 抽数%i" cutoff count)
                .Append(String.Join(" ", ret))
                .ToString()

        msgArg.QuickMessageReply(reply)