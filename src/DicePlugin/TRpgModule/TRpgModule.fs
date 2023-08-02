namespace KPX.DicePlugin.TRpgModule.TRpgModule

open System

open KPX.FsCqHttp.Event.Message
open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Api.Private
open KPX.FsCqHttp.Testing
open KPX.FsCqHttp.Utils.TextResponse

open KPX.TheBot.Host.Utils.Dicer
open KPX.DicePlugin.DiceModule.Utils.DiceExpression
open KPX.DicePlugin.TRpgModule
open KPX.DicePlugin.TRpgModule.Strings
open KPX.DicePlugin.TRpgModule.Coc7


type TRpgModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethod(".coc7", "coc第七版属性值，随机", "")>]
    [<CommandHandlerMethod("#coc7", "coc第七版属性值，每日更新", "")>]
    member x.HandleCoc7(cmdArg: CommandEventArgs) =
        let isDotCommand = cmdArg.CommandAttrib.Command = ".coc7"

        let seed =
            if isDotCommand then
                DiceSeed.SeedByRandom()
            else
                DiceSeed.SeedByUserDay(cmdArg.MessageEvent)

        let de = DiceExpression(Dicer(seed))

        TextTable(ForceText) {
            $"%s{cmdArg.MessageEvent.DisplayName}的人物作成:"

            AsCols [ Literal "属性"; Literal "值" ]

            [ let mutable sum = 0

              for name, expr in Coc7AttrExpr do
                  let d = de.Eval(expr).Sum |> int
                  sum <- sum + d
                  [ Literal name; Integer d ]

              [ Literal "总计"; Integer sum ] ]

            let job = de.Dicer.GetArrayItem(StringData.ChrJobs)

            let jobStr =
                if isDotCommand then
                    $"推荐职业：%s{job}"
                else
                    $"今日推荐职业：%s{job}"

            jobStr
        }

    [<TestFixture>]
    member x.TestCoc7() =
        let tc = TestContext(x)
        tc.ShouldNotThrow(".coc7")
        tc.ShouldNotThrow("#coc7")

    [<CommandHandlerMethod(".ra", "检定（房规）", "")>]
    [<CommandHandlerMethod(".rc", "检定（规则书）", "")>]
    [<CommandHandlerMethod(".rd", ".r 1D100缩写", "")>]
    [<CommandHandlerMethod(".rh", "常规暗骰", "")>]
    [<CommandHandlerMethod(".r", "常规骰点", "")>]
    member x.HandleRoll(cmdArg: CommandEventArgs) =
        let parser = DiceExpression()

        let operators =
            seq {
                yield! parser.Operators |> Seq.map (fun x -> x.Char)
                yield! seq { '0' .. '9' }
            }
            |> set

        let card = CardManager.tryGetCurrentCard cmdArg.MessageEvent.UserId

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

            if args.Length <> 0 then
                reason <- String.Join(" ", args)

        // rh/r [表达式] [原因]
        | ".rh"
        | ".r" ->
            if cmdArg.CommandName = ".rh" then
                isPrivate <- true

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

            if cmdArg.CommandName = ".ra" then
                offset <- 5

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