module TheBot.Module.DiceModule.TRpgModule

open System
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Handler.CommandHandlerBase

open TheBot.Utils.GenericRPN
open TheBot.Utils.Dicer
open TheBot.Utils.TextTable

open TheBot.Module.DiceModule.Utils

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

        let dp = DiceExpression.DiceExpression(dicer)

        for (name, expr) in attrs do 
            tt.AddRow(name, dp.Eval(expr).Value |> int)

        tt.AddPreTable(sprintf "%s的人物作成:" msgArg.MessageEvent.GetNicknameOrCard)
        using (msgArg.OpenResponse(true)) (fun ret -> ret.Write(tt))