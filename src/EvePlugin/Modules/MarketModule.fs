namespace KPX.EvePlugin.Modules

open System

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Testing
open KPX.FsCqHttp.Utils.TextResponse

open KPX.EvePlugin.Data.Utils
open KPX.EvePlugin.Data.EveType
open KPX.EvePlugin.Data.Process

open KPX.TheBot.Host.DataModel.Recipe
open KPX.TheBot.Host.Utils.RecipeRPN

open KPX.EvePlugin.Utils.EveExpression
open KPX.EvePlugin.Utils.Helpers
open KPX.EvePlugin.Utils.Config
open KPX.EvePlugin.Utils.Data
open KPX.EvePlugin.Utils.Extensions


type EveMarketModule() =
    inherit CommandHandlerBase()

    let data = DataBundle.Instance

    let er = EveExpression()

    [<CommandHandlerMethod("#eve矿物", "查询矿物价格", "")>]
    [<CommandHandlerMethod("#em",
                           "查询物品价格",
                           "可以使用表达式，多个物品需要用+连接。
#em 帝国海军散热槽*5+帝国海军多频晶体 L*1000")>]
    member x.HandleEveMarket(cmdArg: CommandEventArgs) =
        let mutable sumItemPrices = true
        let mutable argOverride = None

        if cmdArg.CommandName = "#eve矿物" then
            argOverride <- Some(MineralNames.Replace(',', '+'))
            sumItemPrices <- false

        let t =
            let str =
                if argOverride.IsSome then
                    argOverride.Value
                else
                    String.Join(" ", cmdArg.HeaderArgs)

            er.Eval(str)

        let att = KPX.EvePlugin.Utils.MarketUtils.EveMarketPriceTable()

        match t with
        | Accumulator a ->
            for mr in a do
                att.AddObject(mr.Item, mr.Quantity)
        | _ -> cmdArg.Abort(InputError, $"求值失败，结果是%A{t}")

        if sumItemPrices then att.EmitTotalRow()

        att.Table

    [<TestFixture>]
    member x.TestEveMarket() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#eve矿物")
        tc.ShouldNotThrow("#em 三钛合金")

        tc.ShouldThrow("#em 三铜合金")
        tc.ShouldThrow("#em 三铁合金")

    [<CommandHandlerMethod("#EVE采矿", "EVE挖矿利润，仅供参考", "")>]
    [<CommandHandlerMethod("#EVE挖矿", "EVE挖矿利润，仅供参考", "", IsHidden = true)>]
    member x.HandleOreMining(_: CommandEventArgs) =
        let mineSpeed = 10.0 // m^3/s
        let refineYield = 0.70

        let getSubTypes (names: string) =
            names.Split(',')
            |> Array.map (fun name ->
                let item = data.GetItem(name)

                let proc =
                    RefineProcessCollection
                        .Instance
                        .GetProcessFor(
                            item
                        )
                        .Original

                // 因为精炼是单对多的过程，所以实际是反着的
                let refinePerSec = mineSpeed / proc.Product.Item.Volume / proc.Product.Quantity

                let price =
                    proc.Materials
                    |> Array.sumBy (fun m -> m.Quantity * refinePerSec * refineYield * m.Item.GetPriceInfo().Sell)

                name, price |> ceil)
            |> Array.sortByDescending snd

        let moon = getSubTypes MoonNames
        let ice = getSubTypes IceNames
        let ore = getSubTypes OreNames
        let tore = getSubTypes TriglavianOreNames

        let rowMax = (max (max ice.Length moon.Length) ore.Length) - 1

        TextTable(ForceImage) {
            ToolWarning
            $"采集能力：%g{mineSpeed} m3/s 精炼效率:%g{refineYield}"

            AsCols [ Literal "矿石"
                     Literal "秒利润" { rightAlign }

                     Literal "冰矿"
                     Literal "秒利润" { rightAlign }

                     Literal "月矿"
                     Literal "秒利润" { rightAlign }

                     Literal "导管"
                     Literal "秒利润" { rightAlign } ]

            [ for i = 0 to rowMax do
                  [ if i < ore.Length then
                        Literal(fst ore.[i])
                        Integer(snd ore.[i])
                    else
                        LeftPad
                        RightPad

                    if i < ice.Length then
                        Literal(fst ice.[i])
                        Integer(snd ice.[i])
                    else
                        LeftPad
                        RightPad

                    if i < moon.Length then
                        Literal(fst moon.[i])
                        Integer(snd moon.[i])
                    else
                        LeftPad
                        RightPad

                    if i < tore.Length then
                        Literal(fst tore.[i])
                        Integer(snd tore.[i])
                    else
                        LeftPad
                        RightPad ] ]
        }


    [<TestFixture>]
    member x.TestOreMining() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#eve采矿")
        tc.ShouldNotThrow("#eve挖矿")
