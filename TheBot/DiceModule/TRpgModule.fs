module TheBot.Module.DiceModule.TRpgModule

open System

open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

open TheBot.Utils.Dicer
open TheBot.Utils.HandlerUtils

open TheBot.Module.DiceModule.Utils.DiceExpression
open TheBot.Module.DiceModule.TRpgUtils

type TRpgModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("coc7", "", "", AltCommandStart = ".")>]
    [<CommandHandlerMethodAttribute("coc7", "", "", IsHidden = true)>]
    member x.HandleCoc7(msgArg : CommandArgs) =
        let attrs = [|  "力量", "3D6*5"
                        "体质", "3D6*5"
                        "体型", "(2D6+6)*5"
                        "敏捷", "3D6*5"
                        "外貌", "3D6*5"
                        "智力", "(2D6+6)*5"
                        "意志", "3D6*5"
                        "教育", "(2D6+6)*5"
                        "幸运", "3D6*5"   |]

        let tt = TextTable.FromHeader([|"属性"; "值"|])

        let dicer = Dicer(SeedOption.SeedByUserDay(msgArg.MessageEvent))

        let dp = DiceExpression(dicer)

        let mutable sum = 0
        for (name, expr) in attrs do 
            let d = dp.Eval(expr).Sum |> int
            sum <- sum + d
            tt.AddRow(name, d)
        tt.AddRow("总计", sum)

        tt.AddPreTable(sprintf "%s的人物作成:" msgArg.MessageEvent.DisplayName)

        let job = dicer.GetRandomItem(StringData.[DATA_JOBS])

        tt.AddPostTable(sprintf "今日推荐职业：%s" job)

        using (msgArg.OpenResponse()) (fun ret -> ret.Write(tt))

    [<CommandHandlerMethodAttribute("sc", "理智检定 a/b san", "", AltCommandStart = ".")>]
    member x.HandleSanCheck(msgArg : CommandArgs) = 
        let args = msgArg.Arguments // 参数检查
        if args.Length <> 2 then msgArg.AbortExecution(InputError, "此指令需要2个参数")
        if not <| args.[0].Contains("/") then msgArg.AbortExecution(InputError, "参数1错误")

        let parseSucc, currentSan = Int32.TryParse(args.[1])
        if not parseSucc then msgArg.AbortExecution(InputError, "参数2错误")

        let succ, fail = 
            let s = args.[0].Split("/")
            s.[0], s.[1]

        let de = DiceExpression()
        let check = de.Eval("1D100").Sum |> int
        let status, lose = 
            match check with
            | 1 -> 
                "大成功", DiceExpression.ForceMinDiceing.Eval(succ).Sum |> int
            | 100 ->
                "大失败", DiceExpression.ForceMaxDiceing.Eval(fail).Sum |> int
            | _ when check <= currentSan ->
                "成功", de.Eval(succ).Sum |> int
            | _ -> 
                "失败", de.Eval(fail).Sum |> int

        use ret = msgArg.OpenResponse(ForceText)
        ret.WriteLine("1D100 = {0}：{1}", check, status)
        ret.WriteLine("San值减少{0}点，当前剩余{1}点。", lose, max 0 (currentSan - lose))

    [<CommandHandlerMethodAttribute("rd", ".r 1D100缩写", "", AltCommandStart = ".")>]
    [<CommandHandlerMethodAttribute("rh", "常规暗骰", "", AltCommandStart = ".")>]
    [<CommandHandlerMethodAttribute("r", "常规骰点", "", AltCommandStart = ".")>]
    member x.HandleDice(msgArg : CommandArgs) =
        let parser = DiceExpression()

        let operators =
            seq { yield! parser.Operatos |> Seq.map (fun x -> x.Char)
                  yield! seq {'0' .. '9'} }
            |> set

        let expr, reason = 
            match msgArg.CommandName, msgArg.Arguments.Length with
            | "rd", 0 -> "1D100", "--"
            | "rd", _ -> "1D100", String.Join(" ", msgArg.Arguments)

            | "r", 0 | "rh", 0 ->
                "1D100", "--"
            | "r", 1 | "rh", 1 ->
                let arg = msgArg.Arguments.[0]
                if arg |> String.forall (fun c -> operators.Contains(c)) then
                    arg, "--"
                else
                    "1D100", arg

            | _ -> msgArg.Arguments.[0], String.Join(" ", msgArg.Arguments.[1..])

        match parser.TryEval(expr) with
        | Error e -> msgArg.QuickMessageReply(sprintf "对 %s 求值失败：%s" expr e.Message)
        | Ok i ->
            let msg = sprintf "%s 对 %s 投掷出%s = %O" msgArg.MessageEvent.DisplayName reason expr i
            if msgArg.CommandName = "rh" then
                let ret = Message.Message()
                ret.Add(msg)
                let api = KPX.FsCqHttp.Api.MsgApi.SendPrivateMsg(msgArg.MessageEvent.UserId, ret)
                msgArg.ApiCaller.CallApi(api)
            else
                msgArg.QuickMessageReply(msg)


    [<CommandHandlerMethodAttribute("li", "总结疯狂症状", "", AltCommandStart = ".")>]
    [<CommandHandlerMethodAttribute("ti", "临时疯狂症状", "", AltCommandStart = ".")>]
    member x.HandleTemporaryInsanity(msgArg : CommandArgs) = 
        let de = DiceExpression(Dicer.RandomDicer)

        let key = 
            match msgArg.CommandName with
            | "li" -> "总结症状"
            | "ti" -> "即时症状"
            | unk -> failwithf "不应匹配到的命令名:%s" unk

        let tmpl = de.Dicer.GetRandomItem(StringData.[key])
        msgArg.QuickMessageReply(ParseTemplate(tmpl, de))
        
    [<CommandHandlerMethodAttribute("test", "", "", AltCommandStart = ".")>]
    member x.HandleDiceTest(msgArg : CommandArgs) =
        msgArg.EnsureSenderOwner()

        let template = String.Join(" ", msgArg.Arguments)
        let ret = ParseTemplate(template, DiceExpression(Dicer.RandomDicer))
        msgArg.QuickMessageReply(ret)

