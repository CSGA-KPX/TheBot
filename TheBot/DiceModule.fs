module TheBot.Module.DiceModule

open System
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Handler.CommandHandlerBase

open TheBot.Utils.GenericRPN
open TheBot.Utils.Dicer
open TheBot.Utils.TextTable

module ChoiceHelper =
    open System.Text.RegularExpressions
    open System.Collections.Generic

    let YesOrNoRegex = Regex("(.*)(.+)([没不]\2)(.*)", RegexOptions.Compiled)

    type MessageParser() = 
        
        static let dateTable = 
            [|
                "大后天", TimeSpan.FromDays(3.0)
                "大前天", TimeSpan.FromDays(-2.0)
                "明天", TimeSpan.FromDays(1.0)
                "后天", TimeSpan.FromDays(2.0)
                "昨天", TimeSpan.FromDays(-1.0)
                "前天", TimeSpan.FromDays(-1.0)
                "", TimeSpan.Zero
            |]

        member val AllowAtUser = true with get, set

        member x.Parse(arg : CommandArgs) =
            let atUser = 
                arg.MessageEvent.Message.GetAts()
                |> Array.tryHead
                |> Option.filter (fun _ -> x.AllowAtUser)

            [
                for arg in arg.Arguments do 
                    let seed = List<SeedOption>()
                    let choices = List<string>()

                    if atUser.IsSome then 
                        let at = atUser.Value
                        if at = Message.AtUserType.All then invalidOp "at全体无效"
                        seed.Add(SeedOption.SeedCustom(atUser.Value.ToString()))

                    //拆分条目
                    let m = YesOrNoRegex.Match(arg)
                    if m.Success then
                        choices.Add(m.Groups.[1].Value + m.Groups.[2].Value + m.Groups.[4].Value)
                        choices.Add(m.Groups.[1].Value + m.Groups.[3].Value + m.Groups.[4].Value)
                    else
                        choices.Add(arg)


                    //处理选项
                    for c in choices do 
                        let seed = 
                            [|
                                yield! seed
                                let (_, ts) = dateTable |> Array.find (fun x -> c.Contains(fst x))
                                yield SeedOption.SeedCustom((GetCstTime() + ts).ToString("yyyyMMdd"))
                            |]
                        yield seed, c
            ]

module DiceExpression =
    open System.Text
    open System.Globalization

    type DicerOperand(i : float) =
        member x.Value = i

        interface IOperand<DicerOperand> with
            override l.Add(r) = DicerOperand(l.Value + r.Value)
            override l.Sub(r) = DicerOperand(l.Value - r.Value)
            override l.Div(r) = DicerOperand(l.Value / r.Value)
            override l.Mul(r) = DicerOperand(l.Value * r.Value)

        override x.ToString() = 
            let fmt =
                if (i % 1.0) <> 0.0 then "{0:N2}"
                else "{0:N0}"
            System.String.Format(fmt, i)

    type DiceExpression() as x =
        inherit GenericRPNParser<DicerOperand>()

        let dicer = Dicer(SeedOption.SeedRandom)

        do
            let func (l : DicerOperand) (r : DicerOperand) =
                let ret = Array.init<int> (l.Value |> int) (fun _ -> dicer.GetRandom(r.Value |> uint32)) |> Array.sum
                DicerOperand(ret |> float)

            x.Operatos.Add(GenericOperator<_>('D', Int32.MaxValue, func))
            x.Operatos.Add(GenericOperator<_>('d', Int32.MaxValue, func))

        override x.Tokenize(token) =
            (*[| let strs = tokenRegex.Split(str) |> Array.filter (fun x -> x <> "")
               for str in strs do
                   match str with
                   | _ when Char.IsDigit(str.[0]) -> yield Operand(DicerOperand(Double.Parse(str, NumberStyles.Number)))
                   | _ when x.Operatos.Contains(str) -> yield Operator(x.Operatos.[str])
                   | _ -> failwithf "无法解析 %s" str |]*)

            match token with
            | _ when String.forall Char.IsDigit token ->
                Operand(DicerOperand(Double.Parse(token, NumberStyles.Number)))
            | _ ->
                failwithf "无法解析 %s" token

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
                if msgArg.Command.Value = "#cc" then Array.singleton SeedOption.SeedRandom 
                else
                    if atUser.IsSome then SeedOption.SeedByAtUserDay(msgArg.MessageEvent)
                    else SeedOption.SeedByUserDay(msgArg.MessageEvent)
            let dicer = Dicer(seed, AutoRefreshSeed = false)
            (c, dicer.GetRandomFromString(c, 100u)))
        |> Array.sortBy snd
        |> Array.iter (fun (c, n) -> tt.AddRow((sprintf "%03i" n), c))

        sw.Write(tt.ToString())
        msgArg.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("jrrp", "今日人品值", "")>]
    member x.HandleJrrp(msgArg : CommandArgs) =
        let dicer = Dicer(SeedOption.SeedByUserDay(msgArg.MessageEvent))
        let jrrp = dicer.GetRandom(100u)
        msgArg.QuickMessageReply(sprintf "%s今日人品值是%i" msgArg.MessageEvent.GetNicknameOrCard jrrp)

    [<CommandHandlerMethodAttribute("cal", "计算器", "")>]
    member x.HandleCalculator(msgArg : CommandArgs) =
        let sw = new IO.StringWriter()
        let parser = DiceExpression.DiceExpression()
        for arg in msgArg.Arguments do
            let ret = parser.TryEval(arg)
            match ret with
            | Error e -> sw.WriteLine("对{0}求值失败：{1}", arg, e.Message)
            | Ok i -> sw.WriteLine("{0} = {1}", arg, i.ToString())
        msgArg.QuickMessageReply(sw.ToString())

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
                .AppendLine(String.Join(" ", ret))
                .ToString()

        msgArg.QuickMessageReply(reply)