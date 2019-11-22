module TheBot.Module.TestModule

open KPX.FsCqHttp.Handler.CommandHandlerBase

type TestModule() =
    inherit CommandHandlerBase()
    (*
    [<CommandHandlerMethodAttribute("#test", "显示一条测试信息", "")>]
    member x.HandleTest(msgArg : CommandArgs) =
        msgArg.CqEventArgs.QuickMessageReply("Test success!")

    [<CommandHandlerMethodAttribute("#test.coc7d", "显示一条测试信息", "")>]
    member x.HandleCOC7D(msgArg : CommandArgs) =
        let sw = new System.IO.StringWriter()
        let dicer = new Utils.Dicer()
        let parser = new DiceModule.DiceExpression.DiceExpression()
        sw.WriteLine("本次投掷种子为:{0}", dicer.InitialSeed)
        let conf = 
            [|
                "STR", "3D6*5"
                "CON", "3D6*5"
                "SIZ", "(2D6+6)*5"
                "DEX", "3D6*5"
                "APP", "3D6*5"
                "INT", "(2D6+6)*5"
                "POW", "3D6*5"
                "EDU", "(2D6+6)*5"
                "LUCK","3D6*5"
            |]
            |> Array.map (fun (name, expr) ->
                let ret = parser.Eval(expr, dicer)
                (name, ret)
            )
        for (name, value) in conf do 
            sw.WriteLine("{0} : {1}", name, value.Value)
        sw.WriteLine("总计{0}点", conf |> Array.sumBy (fun (_,v) -> v.Value))
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())*)

