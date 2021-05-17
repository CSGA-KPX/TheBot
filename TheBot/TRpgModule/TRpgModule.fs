﻿namespace KPX.TheBot.Module.TRpgModule.TRpgModule

open System

open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Event
open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Api.Private
open KPX.FsCqHttp.Testing

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

open KPX.TheBot.Utils.Dicer

open KPX.TheBot.Module.DiceModule.Utils.DiceExpression

open KPX.TheBot.Module.TRpgModule
open KPX.TheBot.Module.TRpgModule.Strings
open KPX.TheBot.Module.TRpgModule.Coc7
open KPX.TheBot.Module.TRpgModule.Coc7.DailySan


type TRpgModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute(".coc7", "coc第七版属性值，随机", "")>]
    [<CommandHandlerMethodAttribute("#coc7", "coc第七版属性值，每日更新", "", IsHidden = true)>]
    member x.HandleCoc7(cmdArg : CommandEventArgs) =
        let isDotCommand = cmdArg.CommandAttrib.Command = ".coc7"

        let tt = TextTable("属性", "值")

        let seed =
            if isDotCommand then
                Array.singleton SeedOption.SeedRandom
            else
                SeedOption.SeedByUserDay(cmdArg.MessageEvent)

        let de = DiceExpression(Dicer(seed))

        let mutable sum = 0

        for (name, expr) in Coc7Utils.Coc7AttrExpr do
            let d = de.Eval(expr).Sum |> int
            sum <- sum + d
            tt.AddRow(name, d)

        tt.AddRow("总计", sum)

        tt.AddPreTable(sprintf "%s的人物作成:" cmdArg.MessageEvent.DisplayName)

        let job =
            de.Dicer.GetArrayItem(StringData.ChrJobs)

        if isDotCommand then
            tt.AddPostTable(sprintf "推荐职业：%s" job)
        else
            tt.AddPostTable(sprintf "今日推荐职业：%s" job)

        using (cmdArg.OpenResponse()) (fun ret -> ret.Write(tt))

    [<TestFixture>]
    member x.TestCoc7() = 
        let tc = TestContext(x)
        tc.ShouldNotThrow(".coc7")
        tc.ShouldNotThrow("#coc7")

    [<CommandHandlerMethodAttribute(".sc",
                                    "理智检定 .sc 成功/失败 [当前san]",
                                    "如果没有定义当前san，则从#coc7结果车卡计算。",
                                    IsHidden = true)>]
    member x.HandleSanCheck(cmdArg : CommandEventArgs) =
        let args = cmdArg.Arguments // 参数检查

        if args.Length = 0 || args.Length > 2 then
            cmdArg.Abort(InputError, "此指令需要1/2个参数 .sc 成功/失败 [当前san]")

        if not <| args.[0].Contains("/") then
            cmdArg.Abort(InputError, "成功/失败 表达式错误")

        let isDaily, currentSan =
            if args.Length = 2 then
                let parseSucc, currentSan = Int32.TryParse(args.[1])

                if parseSucc then
                    false, currentSan
                else
                    true, DailySanCacheCollection.Instance.GetValue(cmdArg)
            else
                true, DailySanCacheCollection.Instance.GetValue(cmdArg)

        let succ, fail =
            let s = args.[0].Split("/")
            s.[0], s.[1]

        let de = DiceExpression()
        let check = de.Eval("1D100").Sum |> int

        let status, lose =
            match check with
            | 1 ->
                "大成功",
                DiceExpression.ForceMinDiceing.Eval(succ).Sum
                |> int
            | 100 ->
                "大失败",
                DiceExpression.ForceMaxDiceing.Eval(fail).Sum
                |> int
            | _ when check <= currentSan -> "成功", de.Eval(succ).Sum |> int
            | _ -> "失败", de.Eval(fail).Sum |> int

        use ret = cmdArg.OpenResponse(ForceText)
        ret.WriteLine("1D100 = {0}：{1}", check, status)

        let finalSan = max 0 (currentSan - lose)

        if isDaily then
            DailySanCacheCollection.Instance.SetValue(cmdArg, finalSan)
            ret.WriteLine("今日San值减少{0}点，当前剩余{1}点。", lose, finalSan)
        else
            ret.WriteLine("San值减少{0}点，当前剩余{1}点。", lose, finalSan)

    [<TestFixture>]
    member x.TestSc() = 
        let tc = TestContext(x)
        tc.ShouldNotThrow(".sc 100/100")
        tc.ShouldNotThrow(".sc 100/100 50")
        tc.ShouldNotThrow(".sc 1D10/1D100")
        tc.ShouldNotThrow(".sc 1D10/1D100 50")

    [<CommandHandlerMethodAttribute(".en", "技能/属性成长检定 .en 技能 成功率", "")>]
    member x.HandleEn(cmdArg : CommandEventArgs) =
        let current = ref 0

        match cmdArg.Arguments with
        | [| attr; value |] when Int32.TryParse(value, current) ->
            use ret = cmdArg.OpenResponse()

            let dice = DiceExpression()
            let roll0 = dice.Eval("1D100").Sum |> int
            let usrName = cmdArg.MessageEvent.DisplayName


            if roll0 > !current then // 成功
                ret.WriteLine("{0} 对 {1} 的增强或成长鉴定： {{1D100 = {2}}} -> 成功", usrName, attr, roll0)
                let add = dice.Eval("1D10").Sum |> int

                ret.WriteLine(
                    "其 {1} 增加了 {{1D10 = {2}}} 点，当前为{3} 点",
                    usrName,
                    attr,
                    add,
                    !current + add
                )

                if !current < 90 && !current + add >= 90 then
                    let sanAdd = dice.Eval("2D6").Sum |> int
                    ret.WriteLine("（可选）并为恢复了 {{2D6 = {0}}} 点理智", sanAdd)
            else
                ret.WriteLine("{0} 对 {1} 的增强或成长鉴定： {{1D100 = {2}}} -> 失败", usrName, attr, roll0)
        | _ -> cmdArg.Abort(InputError, "参数错误：.ra/.rc 属性/技能名 属性/技能值")

    [<TestFixture>]
    member x.TestEn() = 
        let tc = TestContext(x)
        tc.ShouldNotThrow(".en 测试 0")
        tc.ShouldNotThrow(".en 测试 100")

    [<CommandHandlerMethodAttribute(".ra", "检定（房规）", "")>]
    [<CommandHandlerMethodAttribute(".rc", "检定（规则书）", "")>]
    [<CommandHandlerMethodAttribute(".rd", ".r 1D100缩写", "")>]
    [<CommandHandlerMethodAttribute(".rh", "常规暗骰", "")>]
    [<CommandHandlerMethodAttribute(".r", "常规骰点", "")>]
    member x.HandleRoll(cmdArg : CommandEventArgs) =
        let parser = DiceExpression()

        let operators =
            seq {
                yield! parser.Operatos |> Seq.map (fun x -> x.Char)
                yield! seq { '0' .. '9' }
            }
            |> set

        let mutable expr = "1D100"
        let mutable needDescript = None
        let mutable isPrivate = false
        let mutable reason = "--"
        let mutable offset = 1

        match cmdArg.CommandName with
        // rd [原因]
        | ".rd" ->
            let args = cmdArg.Arguments
            if args.Length <> 0 then reason <- String.Join(" ", args)

        // rh/r [表达式] [原因]
        | ".rh"
        | ".r" ->
            if cmdArg.CommandName = ".rh" then isPrivate <- true

            match cmdArg.Arguments with
            | [||] -> ()
            | [| arg |] when String.forall (fun c -> operators.Contains(c)) arg -> expr <- arg
            | [| arg |] -> reason <- arg
            | args ->
                expr <- args.[0]
                reason <- String.Join(" ", args.[1..])

        // ra/rc [原因] [阈值]
        | ".ra"
        | ".rc" ->
            let t = ref 0
            if cmdArg.CommandName = ".ra" then offset <- 5

            match cmdArg.Arguments with
            | [| attName; value |] when Int32.TryParse(value, t) ->
                reason <- attName
                needDescript <- Some !t
            | _ -> cmdArg.Abort(InputError, "参数错误：.ra/.rc 属性/技能名 属性/技能值")

        // 其他指令（不存在）
        | _ -> cmdArg.Abort(ModuleError, "指令错误")

        match parser.TryEval(expr) with
        | Error e -> cmdArg.Reply(sprintf "对 %s 求值失败：%s" expr e.Message)
        | Ok i ->
            let rolled = i.Sum |> int
            let usrName = cmdArg.MessageEvent.DisplayName

            let msg =
                if needDescript.IsSome then
                    let desc =
                        RollResultRule(offset)
                            .Descript(rolled, needDescript.Value)

                    sprintf "%s 对 %s 投掷出了{%s = %i} -> %O" usrName reason expr rolled desc
                else
                    sprintf "%s 对 %s 投掷出了{%s = %i}" usrName reason expr rolled

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

    [<CommandHandlerMethodAttribute(".crule", "查询/设置当前房规区间（不稳定）", "", Disabled = true)>]
    member x.HandleRollRule(cmdArg : CommandEventArgs) =
        if cmdArg.MessageEvent.IsPrivate then
            cmdArg.Abort(InputError, "此指令仅私聊无效")


        // 属性/技能名 属性/技能值
        let test = ref 1

        match cmdArg.Arguments with
        | [||] -> ()
        | [| value |] when Int32.TryParse(value, test) ->

            ()
        | _ -> cmdArg.Abort(InputError, "参数错误：.crule 区间偏移值")

        ()

    [<CommandHandlerMethodAttribute(".li", "总结疯狂症状", "")>]
    [<CommandHandlerMethodAttribute(".ti", "临时疯狂症状", "")>]
    member x.HandleInsanity(cmdArg : CommandEventArgs) =
        let key =
            match cmdArg.CommandName with
            | ".li" -> StringData.Key_LI
            | ".ti" -> StringData.Key_TI
            | unk -> failwithf "不应匹配到的命令名:%s" unk

        let de = DiceExpression(Dicer.RandomDicer)

        let tmpl =
            de.Dicer.GetArrayItem(StringData.GetLines(key))

        let ret =
            TrpgStringTemplate(de).ParseTemplate(tmpl)

        cmdArg.Reply(ret)

    [<CommandHandlerMethodAttribute(".bg", "生成人物背景", "")>]
    member x.HandleChrBackground(cmdArg : CommandEventArgs) =

        let de = DiceExpression(Dicer.RandomDicer)

        let ret =
            TrpgStringTemplate(de)
                .ParseByKey(StringData.Key_ChrBackground)

        cmdArg.Reply(ret)

    [<CommandHandlerMethodAttribute(".name", "生成人物背景", "")>]
    member x.HandleChrName(cmdArg : CommandEventArgs) =
        let opt = NameOption()
        opt.Parse(cmdArg)

        if opt.NameCount > 20 then
            cmdArg.Abort(InputError, "数量太多")

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

(*
    [<CommandHandlerMethodAttribute("st", "设置人物卡", "", AltCommandStart = ".", IsHidden = true)>]
    member x.HandleDiceST(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()

        let rx =
            Text.RegularExpressions.Regex(@"([^\s\|0-9]+)([0-9]+)")

        let input = String.Join("", cmdArg.Arguments)

        let chr =
            { CharacterCard.Id = 0L
              CharacterCard.UserId = cmdArg.MessageEvent.UserId
              CharacterCard.ChrName = "无名氏"
              CharacterCard.Props = Collections.Generic.Dictionary<string, int>() }

        // 两种可能：完整字段或者基础9维+变动字段
        for kv in Coc7.DefaultSkillValues do
            chr.[kv.Key] <- kv.Value

        for m in rx.Matches(input) do
            let name = m.Groups.[1].Value
            let prop = name |> Coc7.MapCoc7SkillName
            chr.[prop] <- m.Groups.[2].Value |> int

        let cardCount =
            CountUserCard(cmdArg.MessageEvent.UserId)

        if cardCount > MAX_USER_CARDS
        then cmdArg.AbortExecution(InputError, "人物卡数量上限，你已经有{0}张，上限为{1}张。", cardCount, MAX_USER_CARDS)

        if CardExists(chr) then cmdArg.AbortExecution(InputError, "存在尚未命名的人物卡，请命名后再创建")

        InsertCard(chr)

        using
            (cmdArg.OpenResponse(ForceImage))
            (fun ret ->
                let tt = chr.ToTextTable()
                tt.AddPreTable("已保存人物卡：")
                ret.Write(tt))

    [<CommandHandlerMethodAttribute("pc", "人物卡管理", "", AltCommandStart = ".", IsHidden = true)>]
    member x.HandlePC(cmdArg : CommandEventArgs) =
        match cmdArg.Arguments |> Array.tryItem 0 with
        | None -> cmdArg.QuickMessageReply("list/use/rename")
        | Some (cmd) ->
            match cmd.ToLowerInvariant() with

            | "list" ->
                let cards = cmdArg.GetChrCards()
                use ret = new IO.StringWriter()

                if cards.Length = 0 then
                    ret.WriteLine("没有已录入的角色卡")
                else
                    ret.WriteLine("{0} 当前角色卡有：", cmdArg.MessageEvent.DisplayName)

                    for card in cards do
                        ret.WriteLine(card.ChrName)

                cmdArg.QuickMessageReply(ret.ToString())

            | "use" ->
                let cardName = cmdArg.Arguments |> Array.tryItem 1

                if cardName.IsNone then cmdArg.AbortExecution(InputError, "缺少参数：角色卡")

                let card =
                    cmdArg.GetChrCards()
                    |> Array.tryFind (fun c -> c.ChrName = cardName.Value)

                if card.IsNone
                then cmdArg.AbortExecution(InputError, "没有找到名字为{0}的角色卡", cardName.Value)

                SetCurrentCard(cmdArg.MessageEvent.UserId, card.Value)
                cmdArg.QuickMessageReply("已设置")

            | "rename" ->
                let cardName = cmdArg.Arguments |> Array.tryItem 1
                let current = cmdArg.GetChrCard()

                UpsertCard(
                    { current with
                          ChrName = cardName.Value }
                )

                cmdArg.QuickMessageReply("已保存")

            | "copy" ->
                let cardName =
                    cmdArg.Arguments
                    |> Array.tryItem 1
                    |> Option.defaultValue "新建人物卡"

                let current = cmdArg.GetChrCard()

                InsertCard(
                    { current with
                          Id = 0L
                          ChrName = cardName }
                )

                cmdArg.QuickMessageReply(sprintf "已将%s复制到%s" current.ChrName cardName)

            | "show" ->
                let current = cmdArg.GetChrCard()

                using (cmdArg.OpenResponse(ForceImage)) (fun ret -> ret.Write(current.ToTextTable()))

            | "clr" -> // 删除当前角色卡
                let current = cmdArg.GetChrCard()
                RemoveCard(current)
                cmdArg.QuickMessageReply("已删除")

            | "nuke" -> //删除所有角色卡
                for card in cmdArg.GetChrCards() do
                    RemoveCard(card)

                cmdArg.QuickMessageReply("Booom!")

            | _ -> cmdArg.QuickMessageReply("未知子命令")
*)
