namespace KPX.TheBot.Module.TRpgModule.TRpgModule

open System

open KPX.FsCqHttp.Event.Message
open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Api.Private
open KPX.FsCqHttp.Testing
open KPX.FsCqHttp.Utils.TextResponse

open KPX.TheBot.Utils.Dicer
open KPX.TheBot.Module.DiceModule.Utils.DiceExpression
open KPX.TheBot.Module.TRpgModule
open KPX.TheBot.Module.TRpgModule.Strings
open KPX.TheBot.Module.TRpgModule.Coc7


type TRpgModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethod(".coc7", "coc第七版属性值，随机", "")>]
    [<CommandHandlerMethod("#coc7", "coc第七版属性值，每日更新", "")>]
    member x.HandleCoc7(cmdArg : CommandEventArgs) =
        let isDotCommand = cmdArg.CommandAttrib.Command = ".coc7"

        let seed =
            if isDotCommand then
                Array.singleton SeedOption.SeedRandom
            else
                SeedOption.SeedByUserDay(cmdArg.MessageEvent)

        let de = DiceExpression(Dicer(seed))

        TextTable(ForceText) {
            $"%s{cmdArg.MessageEvent.DisplayName}的人物作成:"

            [ CellBuilder() { literal "属性" }
              CellBuilder() { literal "值" } ]

            [ let mutable sum = 0

              for name, expr in Coc7AttrExpr do
                  let d = de.Eval(expr).Sum |> int
                  sum <- sum + d

                  [ CellBuilder() { literal name }
                    CellBuilder() { integer d } ]

              [ CellBuilder() { literal "总计" }
                CellBuilder() { integer sum } ] ]

            let job =
                de.Dicer.GetArrayItem(StringData.ChrJobs)

            let jobStr =
                if isDotCommand then $"推荐职业：%s{job}" else $"今日推荐职业：%s{job}"

            jobStr
        }

    [<TestFixture>]
    member x.TestCoc7() =
        let tc = TestContext(x)
        tc.ShouldNotThrow(".coc7")
        tc.ShouldNotThrow("#coc7")

    [<CommandHandlerMethod(".sc", "跑团用理智检定 .sc 成功/失败 [当前san]", "如当前san未定义，则取当前角色")>]
    member x.HandleSanCheck(cmdArg : CommandEventArgs) =
        let card =
            CardManager.tryGetCurrentCard cmdArg.MessageEvent.UserId

        let args = cmdArg.HeaderArgs // 参数检查

        if args.Length = 0
           || args.Length > 2
           || (not <| args.[0].Contains("/")) then
            cmdArg.Abort(InputError, $"参数错误，请参考指令帮助 #help %s{cmdArg.CommandAttrib.Command}")

        let san =
            match args.Length with
            | 1 ->
                // 读角色
                if card.IsNone then cmdArg.Abort(InputError, "当前没有绑定角色")

                if card.Value.PropExists("理智") then
                    card.Value.["理智"]
                else
                    cmdArg.Abort(InputError, "角色不存在属性'理智'")
            | 2 ->
                // 给定值
                let parseSucc, value = Int32.TryParse(args.[1])

                if parseSucc then
                    value
                else
                    cmdArg.Abort(InputError, $"无法将%s{args.[1]}转换为整数")
            | _ -> cmdArg.Abort(InputError, $"参数错误，请参考指令帮助 #help %s{cmdArg.CommandAttrib.Command}")

        let succ, fail =
            let s = args.[0].Split("/")
            s.[0], s.[1]

        let de = DiceExpression()
        let check = de.Eval("1D100").Sum |> int

        let status = RollResult.Describe(check, san)

        let lose =
            match status with
            | RollResult.Critical -> DiceExpression.ForceMinDiceing.Eval(succ).Sum
            | RollResult.Fumble -> DiceExpression.ForceMaxDiceing.Eval(fail).Sum
            | _ when status.IsSuccess -> de.Eval(succ).Sum
            | _ -> de.Eval(fail).Sum
            |> int

        let finalSan = max 0 (san - lose)

        use ret = cmdArg.OpenResponse(ForceText)
        ret.WriteLine("1D100 = {0}：{1}", check, status)

        ret.WriteLine("San值减少{0}点，当前剩余{1}点。", lose, finalSan)

        if card.IsSome then
            // 写回角色
            card.Value.["理智"] <- finalSan
            CardManager.upsert card.Value

    [<TestFixture>]
    member x.TestSc() =
        let tc = TestContext(x)
        tc.ShouldNotThrow(".sc 100/100 50")
        tc.ShouldNotThrow(".sc 1D10/1D100 50")

    [<CommandHandlerMethod(".en", "技能/属性成长检定 .en 属性/技能名 属性/技能值", "")>]
    member x.HandleEn(cmdArg : CommandEventArgs) =
        let card =
            CardManager.tryGetCurrentCard cmdArg.MessageEvent.UserId

        let mutable propName = ""
        let current = ref 0

        match cmdArg.HeaderArgs with
        | [| prop |] when card.IsSome ->
            if card.Value.PropExists(prop) then
                propName <- prop
                current := card.Value.[prop]
            else
                cmdArg.Abort(InputError, "角色中不存在指定属性")
        | [| prop; value |] when Int32.TryParse(value, current) -> propName <- prop
        | _ -> cmdArg.Abort(InputError, "参数错误")

        use ret = cmdArg.OpenResponse(ForceText)

        let dice = DiceExpression()
        let roll0 = dice.Eval("1D100").Sum |> int

        let usrName =
            if card.IsSome then
                card.Value.ChrName
            else
                cmdArg.MessageEvent.DisplayName

        if roll0 > !current then // 成功
            ret.WriteLine("{0} 对 {1} 的增强或成长鉴定： {{1D100 = {2}}} -> 成功", usrName, propName, roll0)
            let add = dice.Eval("1D10").Sum |> int

            ret.WriteLine(
                "其 {1} 增加了 {{1D10 = {2}}} 点，当前为{3} 点",
                usrName,
                propName,
                add,
                !current + add
            )

            if card.IsSome then card.Value.[propName] <- !current + add

            if !current < 90 && !current + add >= 90 then
                let sanAdd = dice.Eval("2D6").Sum |> int
                ret.WriteLine("并为恢复了 {{2D6 = {0}}} 点理智", sanAdd)

                if card.IsSome then
                    card.Value.["理智"] <- card.Value.["理智"] + sanAdd

            if card.IsSome then CardManager.upsert card.Value
        else
            ret.WriteLine("{0} 对 {1} 的增强或成长鉴定： {{1D100 = {2}}} -> 失败", usrName, propName, roll0)

    [<TestFixture>]
    member x.TestEn() =
        let tc = TestContext(x)
        tc.ShouldNotThrow(".en 测试 0")
        tc.ShouldNotThrow(".en 测试 100")

    [<CommandHandlerMethod(".ra", "检定（房规）", "")>]
    [<CommandHandlerMethod(".rc", "检定（规则书）", "")>]
    [<CommandHandlerMethod(".rd", ".r 1D100缩写", "")>]
    [<CommandHandlerMethod(".rh", "常规暗骰", "")>]
    [<CommandHandlerMethod(".r", "常规骰点", "")>]
    member x.HandleRoll(cmdArg : CommandEventArgs) =
        let parser = DiceExpression()

        let operators =
            seq {
                yield! parser.Operators |> Seq.map (fun x -> x.Char)
                yield! seq { '0' .. '9' }
            }
            |> set

        let card =
            CardManager.tryGetCurrentCard cmdArg.MessageEvent.UserId

        let mutable useCardName = false
        let mutable expr = "1D100"
        let mutable needDescription = None
        let mutable isPrivate = false
        let mutable reason = "--"
        let mutable offset = 1

        match cmdArg.CommandName with
        // rd [原因]
        | ".rd" ->
            let args = cmdArg.HeaderArgs
            if args.Length <> 0 then reason <- String.Join(" ", args)

        // rh/r [表达式] [原因]
        | ".rh"
        | ".r" ->
            if cmdArg.CommandName = ".rh" then isPrivate <- true

            match cmdArg.HeaderArgs with
            | [||] -> ()
            | [| arg |] when String.forall operators.Contains arg -> expr <- arg
            | [| arg |] -> reason <- arg
            | args ->
                expr <- args.[0]
                reason <- String.Join(" ", args.[1..])

        // ra/rc [原因] [阈值]
        | ".ra"
        | ".rc" ->
            let t = ref 0
            if cmdArg.CommandName = ".ra" then offset <- 5

            match cmdArg.HeaderArgs with
            | [| attName |] when card.IsSome ->
                if card.Value.PropExists(attName) then
                    reason <- attName
                    needDescription <- Some card.Value.[attName]
                    useCardName <- true
                else
                    cmdArg.Abort(InputError, "角色中不存在指定属性")
            | [| attName; value |] when Int32.TryParse(value, t) ->
                reason <- attName
                needDescription <- Some !t
            | _ -> cmdArg.Abort(InputError, "参数错误：.ra/.rc 属性/技能名 属性/技能值")

        // 其他指令（不存在）
        | _ -> cmdArg.Abort(ModuleError, "指令错误")

        match parser.TryEval(expr) with
        | Error e -> cmdArg.Reply $"对 %s{expr} 求值失败：%s{e.Message}"
        | Ok i ->
            let rolled = i.Sum |> int

            let usrName =
                if useCardName then
                    card.Value.ChrName
                else
                    cmdArg.MessageEvent.DisplayName

            let msg =
                if needDescription.IsSome then
                    let desc =
                        RollResultRule(offset)
                            .Describe(rolled, needDescription.Value)

                    $"%s{usrName} 对 %s{reason} 投掷出了{{%s{expr} = %i{rolled}}} -> {desc}"
                else
                    $"%s{usrName} 对 %s{reason} 投掷出了{{%s{expr} = %i{rolled}}}"

            if isPrivate then
                let ret = Message()
                ret.Add(msg)

                SendPrivateMsg(cmdArg.MessageEvent.UserId, ret)
                |> cmdArg.ApiCaller.CallApi
                |> ignore
            else
                cmdArg.Reply(msg)

    [<TestFixture>]
    member x.TestR() =
        let tc = TestContext(x)
        tc.ShouldNotThrow(".r")
        tc.ShouldNotThrow(".r 测试")
        tc.ShouldNotThrow(".rd")
        tc.ShouldNotThrow(".rd 测试")
        tc.ShouldNotThrow(".ra 测试 50")
        tc.ShouldNotThrow(".rc 测试 50")
        // 测试表达式
        tc.ShouldNotThrow(".r 1D100 测试")
        tc.ShouldNotThrow(".r 1D(100+100) 测试")
        tc.ShouldNotThrow(".r 50*50 测试")

    [<CommandHandlerMethod(".crule", "查询/设置当前房规区间（不稳定）", "", Disabled = true)>]
    member x.HandleRollRule(cmdArg : CommandEventArgs) =

        if cmdArg.MessageEvent.MessageType = MessageType.Private then
            cmdArg.Abort(InputError, "此指令仅私聊无效")


        // 属性/技能名 属性/技能值
        let test = ref 1

        match cmdArg.HeaderArgs with
        | [||] -> ()
        | [| value |] when Int32.TryParse(value, test) ->

            ()
        | _ -> cmdArg.Abort(InputError, "参数错误：.crule 区间偏移值")

        ()

    [<CommandHandlerMethod(".li", "总结疯狂症状", "")>]
    [<CommandHandlerMethod(".ti", "临时疯狂症状", "")>]
    member x.HandleInsanity(cmdArg : CommandEventArgs) =
        let key =
            match cmdArg.CommandName with
            | ".li" -> StringData.Key_LI
            | ".ti" -> StringData.Key_TI
            | unk -> failwithf $"不应匹配到的命令名:%s{unk}"

        let de = DiceExpression(Dicer.RandomDicer)

        let tmpl =
            de.Dicer.GetArrayItem(StringData.GetLines(key))

        let ret =
            TrpgStringTemplate(de).ParseTemplate(tmpl)

        cmdArg.Reply(ret)

    [<CommandHandlerMethod(".bg", "生成人物背景", "")>]
    member x.HandleChrBackground(cmdArg : CommandEventArgs) =

        let de = DiceExpression(Dicer.RandomDicer)

        let ret =
            TrpgStringTemplate(de)
                .ParseByKey(StringData.Key_ChrBackground)

        cmdArg.Reply(ret)

    [<CommandHandlerMethod(".name", "生成人物背景", "")>]
    member x.HandleChrName(cmdArg : CommandEventArgs) =
        let opt = NameOption()
        opt.Parse(cmdArg.HeaderArgs)

        if opt.NameCount > 20 then cmdArg.Abort(InputError, "数量太多")

        let de = DiceExpression(Dicer.RandomDicer)

        let tmpl =
            StringData.GetString(opt.NameLanguageKey)

        if opt.NonOptionStrings.Count <> 0 then
            let str = opt.GetNonOptionString()
            cmdArg.Abort(InputError, "意外参数'{0}'", str)

        let names =
            Seq.initInfinite (fun _ -> TrpgStringTemplate(de).ParseTemplate(tmpl))
            |> Seq.distinct
            |> Seq.take opt.NameCount

        let ret = String.Join(" ", names)
        cmdArg.Reply(ret)

    [<TestFixture>]
    member x.TestGenerator() =
        let tc = TestContext(x)
        tc.ShouldNotThrow(".li")
        tc.ShouldNotThrow(".ti")
        tc.ShouldNotThrow(".bg")
        tc.ShouldNotThrow(".name 英")
        tc.ShouldNotThrow(".name 英汉")
        tc.ShouldNotThrow(".name 中")
        tc.ShouldNotThrow(".name 日")
        tc.ShouldNotThrow(".name en")
        tc.ShouldNotThrow(".name eng")
        tc.ShouldNotThrow(".name zh")
        tc.ShouldNotThrow(".name chs")
        tc.ShouldNotThrow(".name jp")
        tc.ShouldNotThrow(".name jpn")
        tc.ShouldNotThrow(".name enzh")
        tc.ShouldThrow(".name AAA")
        tc.ShouldThrow(".name BBB")
