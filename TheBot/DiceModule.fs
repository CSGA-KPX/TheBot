module TheBot.Module.DiceModule

open System
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Handler.CommandHandlerBase

open TheBot.Utils.GenericRPN
open TheBot.Utils.Dicer
open TheBot.Utils.UserConfig
open TheBot.Utils.TextTable

module ChoiceHelper =
    open System.Text.RegularExpressions

    let YesOrNoRegex = Regex("(.*)(.+)([没不]\2)(.*)", RegexOptions.Compiled)

module DiceExpression =
    open System.Text.RegularExpressions

    type DicerOperand(i : int) =
        member x.Value = i

        interface IOperand<DicerOperand> with
            override l.Add(r) = DicerOperand(l.Value + r.Value)
            override l.Sub(r) = DicerOperand(l.Value - r.Value)
            override l.Div(r) = DicerOperand(l.Value / r.Value)
            override l.Mul(r) = DicerOperand(l.Value * r.Value)

        override x.ToString() = i.ToString()

    type DiceExpression() as x =
        inherit GenericRPNParser<DicerOperand>()

        let tokenRegex = Regex("([^0-9])", RegexOptions.Compiled)

        do
            x.AddOperator(GenericOperator('D', 5))
            x.AddOperator(GenericOperator('d', 5))

        override x.Tokenize(str) =
            [| let strs = tokenRegex.Split(str) |> Array.filter (fun x -> x <> "")
               for str in strs do
                   match str with
                   | _ when Char.IsDigit(str.[0]) -> yield Operand(DicerOperand(str |> int))
                   | _ when x.Operatos.ContainsKey(str) -> yield Operator(x.Operatos.[str])
                   | _ -> failwithf "无法解析 %s" str |]

        member x.Eval(str : string, dicer : Dicer) =
            let func =
                EvalDelegate<DicerOperand>(fun (c, l, r) ->
                let d = l.Value
                let l = l :> IOperand<DicerOperand>
                match c with
                | '+' -> l.Add(r)
                | '-' -> l.Sub(r)
                | '*' -> l.Mul(r)
                | '/' -> l.Div(r)
                | 'D'
                | 'd' ->
                    let ret = Array.init<int> d (fun _ -> dicer.GetRandom(r.Value |> uint32)) |> Array.sum
                    DicerOperand(ret)
                | _ -> failwithf "")
            x.EvalWith(str, func)

        member x.TryEval(str : string, dicer : Dicer) =
            try
                let ret = x.Eval(str, dicer)
                Ok(ret)
            with e -> Error e

type DiceModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("c", "对多个选项1d100", "A B C D")>]
    member x.HandleChoices(msgArg : CommandArgs) =
        let atUser = msgArg.MessageEvent.Message.GetAts() |> Array.tryHead
        let sw = new IO.StringWriter()
        if atUser.IsSome then
            match atUser.Value with
            | Message.AtUserType.All ->
                failwithf "公共事件请用at bot账号"
            | Message.AtUserType.User x when x = msgArg.CqEventArgs.SelfId ->
                sw.WriteLine("公投：")
            | Message.AtUserType.User x ->
                let atUserName = KPX.FsCqHttp.Api.GroupApi.GetGroupMemberInfo(msgArg.MessageEvent.GroupId, x)
                msgArg.CqEventArgs.CallApi(atUserName)
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
                if atUser.IsSome then SeedOption.SeedByAtUserDay(msgArg.MessageEvent)
                else SeedOption.SeedByUserDay(msgArg.MessageEvent)
            let dicer = Dicer(seed, AutoRefreshSeed = false)
            (c, dicer.GetRandomFromString(c, 100u)))
        |> Array.sortBy snd
        |> Array.iter (fun (c, n) -> tt.AddRow((sprintf "%03i" n), c))

        sw.Write(tt.ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("jrrp", "今日人品值", "")>]
    member x.HandleJrrp(msgArg : CommandArgs) =
        let dicer = Dicer(SeedOption.SeedByUserDay(msgArg.MessageEvent))
        let jrrp = dicer.GetRandom(100u)
        msgArg.CqEventArgs.QuickMessageReply(sprintf "%s今日人品值是%i" msgArg.MessageEvent.GetNicknameOrCard jrrp)

    [<CommandHandlerMethodAttribute("cal", "计算器", "")>]
    member x.HandleCalculator(msgArg : CommandArgs) =
        let sw = new System.IO.StringWriter()
        let dicer = Dicer()
        let parser = DiceExpression.DiceExpression()
        for arg in msgArg.Arguments do
            let ret = parser.TryEval(arg, dicer)
            match ret with
            | Error e -> sw.WriteLine("对{0}失败{1}", arg, e.ToString())
            | Ok i -> sw.WriteLine("对{0}求值得{1}", arg, i.Value)
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

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

        msgArg.CqEventArgs.QuickMessageReply(reply)