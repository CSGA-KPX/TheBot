namespace KPX.TheBot.Module.EveModule

open System

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Testing
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

open KPX.TheBot.Data.CommonModule.Recipe
open KPX.TheBot.Data.EveData.Utils
open KPX.TheBot.Data.EveData.EveType
open KPX.TheBot.Data.EveData.Process

open KPX.TheBot.Utils.RecipeRPN

open KPX.TheBot.Module.EveModule.Utils.Helpers
open KPX.TheBot.Module.EveModule.Utils.Config
open KPX.TheBot.Module.EveModule.Utils.Data
open KPX.TheBot.Module.EveModule.Utils.Extensions


type EveMarketModule() =
    inherit CommandHandlerBase()

    let data = DataBundle.Instance

    let er = EveExpression.EveExpression()

    [<CommandHandlerMethodAttribute("#eve矿物", "查询矿物价格", "")>]
    [<CommandHandlerMethodAttribute("#em", "查询物品价格", "可以使用表达式，多个物品需要用+连接。
#em 帝国海军散热槽*5+帝国海军多频晶体 L*1000")>]
    member x.HandleEveMarket(cmdArg : CommandEventArgs) =
        let mutable argOverride = None

        if cmdArg.CommandName = "#eve矿物" then argOverride <- Some(MineralNames.Replace(',', '+'))

        let t =
            let str =
                if argOverride.IsSome then argOverride.Value else String.Join(" ", cmdArg.Arguments)

            er.Eval(str)

        let att = Utils.MarketUtils.EveMarketPriceTable()

        match t with
        | Accumulator a ->
            for mr in a do
                att.AddObject(mr.Item, mr.Quantity)
        | _ -> cmdArg.Abort(InputError, sprintf "求值失败，结果是%A" t)

        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.Arguments)
        using (cmdArg.OpenResponse(cfg.ResponseType)) (fun x -> x.Write(att))

    [<TestFixture>]
    member x.TestEveMarket() = 
        let tc = TestContext(x)
        tc.ShouldNotThrow("#eve矿物")
        tc.ShouldNotThrow("#em 三钛合金")
        
        tc.ShouldThrow("#em 三铜合金")
        tc.ShouldThrow("#em 三铁合金")

    [<CommandHandlerMethodAttribute("#EVE采矿", "EVE挖矿利润，仅供参考", "")>]
    [<CommandHandlerMethodAttribute("#EVE挖矿", "EVE挖矿利润，仅供参考", "")>]
    member x.HandleOreMining(cmdArg : CommandEventArgs) =
        let mineSpeed = 10.0 // m^3/s
        let refineYield = 0.70

        let tt =
            TextTable(
                "矿石",
                RightAlignCell "秒利润",
                "冰矿",
                RightAlignCell "秒利润",
                "月矿",
                RightAlignCell "秒利润",
                "导管",
                RightAlignCell "秒利润"
            )

        tt.AddPreTable(ToolWarning)
        tt.AddPreTable(sprintf "采集能力：%g m3/s 精炼效率:%g" mineSpeed refineYield)

        let getSubTypes (names : string) =
            names.Split(',')
            |> Array.map
                (fun name ->
                    let item = data.GetItem(name)

                    let proc =
                        RefineProcessCollection
                            .Instance
                            .GetProcessFor(item)
                            .Original

                    let input = proc.Input.[0]

                    let refinePerSec =
                        mineSpeed / input.Item.Volume / input.Quantity

                    let price =
                        proc.Output
                        |> Array.sumBy
                            (fun m ->
                                m.Quantity
                                * refinePerSec
                                * refineYield
                                * m.Item.GetPriceInfo().Sell)

                    name, price |> ceil)
            |> Array.sortByDescending snd

        let moon = getSubTypes MoonNames
        let ice = getSubTypes IceNames
        let ore = getSubTypes OreNames
        let tore = getSubTypes TriglavianOreNames

        let tryGetRow (arr : (string * float) []) (id : int) =
            if id <= arr.Length - 1 then
                let n, p = arr.[id]
                (box n, box <| HumanReadableSig4Int p)
            else
                (box "--", box <| PaddingRight)

        let rowMax =
            (max (max ice.Length moon.Length) ore.Length) - 1

        for i = 0 to rowMax do
            let eon, eop = tryGetRow ore i
            let ein, eip = tryGetRow ice i
            let emn, emp = tryGetRow moon i
            let etn, etp = tryGetRow tore i

            tt.AddRow(eon, eop, ein, eip, emn, emp, etn, etp)

        use ret = cmdArg.OpenResponse(ForceImage)
        ret.Write(tt)

    [<TestFixture>]
    member x.TestOreMining() = 
        let tc = TestContext(x)
        tc.ShouldNotThrow("#eve采矿")
        tc.ShouldNotThrow("#eve挖矿")