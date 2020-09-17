module TheBot.Module.DiceModule.TRpgModule

open System
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Handler.CommandHandlerBase
open KPX.FsCqHttp.Utils.TextTable

open TheBot.Utils.GenericRPN
open TheBot.Utils.Dicer

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

        let mutable sum = 0
        for (name, expr) in attrs do 
            let d = dp.Eval(expr).Value |> int
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