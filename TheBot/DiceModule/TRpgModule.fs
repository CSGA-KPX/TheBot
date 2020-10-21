module TheBot.Module.DiceModule.TRpgModule

open System

open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

open TheBot.Utils.Dicer

open TheBot.Module.DiceModule.Utils.DiceExpression


type TRpgModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("coc7", "", "")>]
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

        let jobs = TheBot.Utils.EmbeddedResource
                    .GetResourceManager("TRpg")
                    .GetString("职业")
                    .Split("\r\n")
        let job = dicer.GetRandomItem(jobs)
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