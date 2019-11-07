module TheBot.Module.DiceModule
open System
open KPX.FsCqHttp.Handler.CommandHandlerBase
open TheBot.Utils

module ChoiceHelper = 
    open System.Text.RegularExpressions
    let YesOrNoRegex = new Regex("(.+)([没不]\1)(.*)", RegexOptions.Compiled)
    let doDice((dicer : Dicer), (opts : string []))=
        opts
        |> Array.map (fun c ->
            (c, dicer.GetRandomFromString(c, 100u)))
        |> Array.sortBy (fun (_, n) -> n)

module DiceExpression = 
    open TheBot.GenericRPN
    open System.Text.RegularExpressions

    type DicerOperand(i : int) = 
        member x.Value = i

        interface IOperand<DicerOperand> with
            override l.Add(r) = new DicerOperand(l.Value + r.Value)
            override l.Sub(r) = new DicerOperand(l.Value - r.Value)
            override l.Div(r) = new DicerOperand(l.Value / r.Value)
            override l.Mul(r) = new DicerOperand(l.Value * r.Value)

        override x.ToString() = 
            i.ToString()

    type DiceExpression() as x = 
        inherit GenericRPNParser<DicerOperand>()

        let tokenRegex = new Regex("([^0-9])", RegexOptions.Compiled)
        do
            x.AddOperator(new GenericOperator('D', 5))
            x.AddOperator(new GenericOperator('d', 5))

        override x.Tokenize(str) = 
            [|
                let strs = tokenRegex.Split(str) |> Array.filter (fun x -> x <> "")
                for str in strs do
                    match str with
                    | _ when Char.IsDigit(str.[0]) ->
                        yield Operand (new DicerOperand(str |> int))
                    | _ when x.Operatos.ContainsKey(str) -> 
                        yield Operator (x.Operatos.[str])
                    | _ -> failwithf "Unknown token %s" str
            |]

        member x.Eval(str : string, dicer : Dicer) = 
            let func = new EvalDelegate<DicerOperand>(fun (c, l, r) ->
                let d = l.Value
                let l = l :> IOperand<DicerOperand>
                match c with
                | '+' -> l.Add(r)
                | '-' -> l.Sub(r)
                | '*' -> l.Mul(r)
                | '/' -> l.Div(r)
                | 'D' | 'd' ->
                    let ret = 
                        Array.init<int> d (fun _ -> dicer.GetRandom(r.Value |> uint32))
                        |> Array.sum
                    new DicerOperand(ret)
                | _ ->  failwithf ""
            )
            x.EvalWith(str, func)

        member x.TryEval(str : string, dicer : Dicer) = 
            try
                let ret = x.Eval(str, dicer)
                Ok (ret)
            with
            |e -> 
                Error e

type DiceModule() = 
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("c", "对多个选项1d100", "A B C D")>]
    member x.HandleChoices(msgArg : CommandArgs) = 
        let atUser = msgArg.MessageEvent.Message.GetAts() |> Array.tryHead
        let seed = 
            if atUser.IsSome then
                SeedOption.SeedByAtUserDay(msgArg.MessageEvent)
            else
                SeedOption.SeedByUserDay(msgArg.MessageEvent)
        let dicer = new Dicer(seed, AutoRefreshSeed = false)
        
        let sw = new IO.StringWriter()
        if atUser.IsSome then
            let atUserId = 
                match atUser.Value with
                | KPX.FsCqHttp.DataType.Message.AtUserType.All ->
                    failwithf ""
                | KPX.FsCqHttp.DataType.Message.AtUserType.User x -> x
            let atUserName = KPX.FsCqHttp.Api.GroupApi.GetGroupMemberInfo(msgArg.MessageEvent.GroupId, atUserId)
            let ret = msgArg.CqEventArgs.CallApi(atUserName)
            sw.WriteLine("{0} 为 {1} 投掷：", msgArg.MessageEvent.GetNicknameOrCard, ret.DisplayName)
        let tt = TextTable.FromHeader([|"1D100"; "选项"|])
        let opts = 
            if msgArg.Arguments.Length = 1 then
                [|
                    let msg = msgArg.Arguments.[0]
                    let   m = ChoiceHelper.YesOrNoRegex.Match(msg)
                    if m.Success then
                        yield m.Groups.[1].Value + m.Groups.[3].Value 
                        yield m.Groups.[2].Value + m.Groups.[3].Value 
                    else
                        yield! msgArg.Arguments
                |]
            else
                msgArg.Arguments
        for (c,n) in ChoiceHelper.doDice(dicer, opts) do 
            tt.AddRow((sprintf "%03i" n), c)
        sw.Write(tt.ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("jrrp", "今日人品值", "")>]
    member x.HandleJrrp(msgArg : CommandArgs) = 
        let dicer = new Dicer(SeedOption.SeedByUserDay(msgArg.MessageEvent))
        let jrrp = dicer.GetRandom(100u)
        msgArg.CqEventArgs.QuickMessageReply(sprintf "%s今日人品值是%i" msgArg.MessageEvent.GetNicknameOrCard jrrp)


    [<CommandHandlerMethodAttribute("cal", "计算器", "")>]
    member x.HandleCalculator(msgArg : CommandArgs) = 
        let sw = new System.IO.StringWriter()
        let dicer = new Dicer()
        let parser = new DiceExpression.DiceExpression()
        for arg in msgArg.Arguments do 
            let  ret = parser.TryEval(arg, dicer)
            match ret with
            | Error e -> 
                sw.WriteLine("对{0}失败{1}", arg, e.ToString())
            | Ok    i ->
                sw.WriteLine("对{0}求值得{1}", arg, i.Value)
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())