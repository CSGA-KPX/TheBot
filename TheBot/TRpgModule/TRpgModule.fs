namespace KPX.TheBot.Module.TRpgModule.TRpgModule

open System

open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Event
open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Api.Private

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

open KPX.TheBot.Utils.Dicer
open KPX.TheBot.Utils.HandlerUtils

open KPX.TheBot.Module.DiceModule.Utils.DiceExpression

open KPX.TheBot.Module.TRpgModule.TRpgUtils
open KPX.TheBot.Module.TRpgModule.TRpgCharacterCard
open KPX.TheBot.Module.TRpgModule.CardManager


type TRpgModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("coc7", "coc第七版", "", AltCommandStart = ".")>]
    [<CommandHandlerMethodAttribute("coc7", "coc第七版", "", IsHidden = true)>]
    member x.HandleCoc7(cmdArg : CommandEventArgs) =
        let isDotCommand = cmdArg.CommandAttrib.CommandStart = "."

        let attrs =
            [| "力量", "3D6*5"
               "体质", "3D6*5"
               "体型", "(2D6+6)*5"
               "敏捷", "3D6*5"
               "外貌", "3D6*5"
               "智力", "(2D6+6)*5"
               "意志", "3D6*5"
               "教育", "(2D6+6)*5"
               "幸运", "3D6*5" |]

        let tt = TextTable("属性", "值")

        let seed =
            if isDotCommand then
                Array.singleton SeedOption.SeedRandom
            else
                SeedOption.SeedByUserDay(cmdArg.MessageEvent)

        let de = DiceExpression(Dicer(seed))

        let mutable sum = 0

        for (name, expr) in attrs do
            let d = de.Eval(expr).Sum |> int
            sum <- sum + d
            tt.AddRow(name, d)

        tt.AddRow("总计", sum)

        tt.AddPreTable(sprintf "%s的人物作成:" cmdArg.MessageEvent.DisplayName)

        let job =
            de.Dicer.GetRandomItem(StringData.ChrJobs)

        if isDotCommand then tt.AddPostTable(sprintf "推荐职业：%s" job) else tt.AddPostTable(sprintf "今日推荐职业：%s" job)

        using (cmdArg.OpenResponse()) (fun ret -> ret.Write(tt))

    [<CommandHandlerMethodAttribute("sc", "理智检定 a/b san", "", AltCommandStart = ".")>]
    member x.HandleSanCheck(cmdArg : CommandEventArgs) =
        let args = cmdArg.Arguments // 参数检查

        if args.Length <> 2 then cmdArg.AbortExecution(InputError, "此指令需要2个参数")

        if not <| args.[0].Contains("/") then cmdArg.AbortExecution(InputError, "参数1错误")

        let parseSucc, currentSan = Int32.TryParse(args.[1])

        if not parseSucc then cmdArg.AbortExecution(InputError, "参数2错误")

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
        ret.WriteLine("San值减少{0}点，当前剩余{1}点。", lose, max 0 (currentSan - lose))

    [<CommandHandlerMethodAttribute("rd", ".r 1D100缩写", "", AltCommandStart = ".")>]
    [<CommandHandlerMethodAttribute("rh", "常规暗骰", "", AltCommandStart = ".")>]
    [<CommandHandlerMethodAttribute("r", "常规骰点", "", AltCommandStart = ".")>]
    member x.HandleDice(cmdArg : CommandEventArgs) =
        let parser = DiceExpression()

        let operators =
            seq {
                yield! parser.Operatos |> Seq.map (fun x -> x.Char)
                yield! seq { '0' .. '9' }
            }
            |> set

        let expr, reason =
            match cmdArg.CommandName, cmdArg.Arguments.Length with
            | "rd", 0 -> "1D100", "--"
            | "rd", _ -> "1D100", String.Join(" ", cmdArg.Arguments)

            | "r", 0
            | "rh", 0 -> "1D100", "--"
            | "r", 1
            | "rh", 1 ->
                let arg = cmdArg.Arguments.[0]

                if arg
                   |> String.forall (fun c -> operators.Contains(c)) then
                    arg, "--"
                else
                    "1D100", arg

            | _ -> cmdArg.Arguments.[0], String.Join(" ", cmdArg.Arguments.[1..])

        match parser.TryEval(expr) with
        | Error e -> cmdArg.QuickMessageReply(sprintf "对 %s 求值失败：%s" expr e.Message)
        | Ok i ->
            let msg =
                sprintf "%s 对 %s 投掷出%s = %O" cmdArg.MessageEvent.DisplayName reason expr i

            if cmdArg.CommandName = "rh" then
                let ret = Message()
                ret.Add(msg)

                let api =
                    SendPrivateMsg(cmdArg.MessageEvent.UserId, ret)

                cmdArg.ApiCaller.CallApi(api)
            else
                cmdArg.QuickMessageReply(msg)


    [<CommandHandlerMethodAttribute("li", "总结疯狂症状", "", AltCommandStart = ".")>]
    [<CommandHandlerMethodAttribute("ti", "临时疯狂症状", "", AltCommandStart = ".")>]
    member x.HandleTemporaryInsanity(cmdArg : CommandEventArgs) =
        let de = DiceExpression(Dicer.RandomDicer)

        let key =
            match cmdArg.CommandName with
            | "li" -> StringData.Key_LI
            | "ti" -> StringData.Key_TI
            | unk -> failwithf "不应匹配到的命令名:%s" unk

        let tmpl =
            de.Dicer.GetRandomItem(StringData.GetLines(key))

        cmdArg.QuickMessageReply(ParseTemplate(tmpl, de))

    [<CommandHandlerMethodAttribute("bg", "", "", AltCommandStart = ".")>]
    member x.HandleChrBackground(cmdArg : CommandEventArgs) =
        let template =
            StringData.GetString(StringData.Key_ChrBackground)

        let ret =
            ParseTemplate(template, DiceExpression(Dicer.RandomDicer))

        cmdArg.QuickMessageReply(ret)


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
