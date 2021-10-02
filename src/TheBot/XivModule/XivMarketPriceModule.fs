namespace KPX.TheBot.Module.XivModule.XivMarketPrice

open System

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.Subcommands
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Testing

open KPX.TheBot.Data.CommonModule.Recipe
open KPX.TheBot.Data.XivData
open KPX.TheBot.Utils.RecipeRPN

open KPX.TheBot.Module.XivModule.Utils
open KPX.TheBot.Module.XivModule.Utils.MarketUtils


type MarketSubcommands =
    | Crystals
    | Materia

    interface ISubcommandTemplate with
        member x.Usage =
            match x with
            | Crystals -> "水晶"
            | Materia -> "魔晶石"


type XivMarketPriceModule() =
    inherit CommandHandlerBase()

    let itemCol = ItemCollection.Instance
    let xivExpr = XivExpression.XivExpression()

    let universalis =
        UniversalisMarketCache.MarketInfoCollection.Instance

    [<TestFixture>]
    member x.TestXivMarket() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#fm 风之水晶")
        tc.ShouldNotThrow("#fm 风之水晶 拉诺西亚")
        tc.ShouldNotThrow("#fm 风之水晶 一区")

    [<CommandHandlerMethod("#fm",
                           "FF14市场查询。可以使用 采集重建/魔晶石/水晶 快捷组",
                           "接受以下参数：
text 以文本格式输出结果
分区/服务器名 调整查询分区下的所有服务器。见#ff14help
#fm 一区 风之水晶 text:
#fm 拉诺 紫水 风之水晶")>]
    member x.HandleXivMarket(cmdArg : CommandEventArgs) =
        let opt = CommandUtils.XivOption()
        opt.Parse(cmdArg.HeaderArgs)

        let acc = XivExpression.ItemAccumulator()
        let worlds = opt.World.Values

        // 扩展子指令的物品表
        match SubcommandParser.Parse<MarketSubcommands>(cmdArg.HeaderArgs) with
        | None when opt.NonOptionStrings.Count = 0 -> cmdArg.Abort(InputError, "没有输入物品或子指令名称")
        | None ->
            for str in opt.NonOptionStrings do
                match xivExpr.TryEval(str) with
                | Error err -> raise err
                | Ok (Number i) -> cmdArg.Abort(InputError, $"计算结果为数字%f{i}，物品Id请加#")
                | Ok (Accumulator a) ->
                    for i in a do
                        acc.Update(i)
        | Some Crystals ->
            if worlds.Length >= 2 then
                cmdArg.Abort(InputError, "该指令不支持多服务器")

            for itemId = 2 to 19 do
                let item = itemCol.GetByItemId(itemId)
                acc.Update(item)
        | Some Materia ->
            let ret =
                opt.NonOptionStrings
                |> Seq.tryItem 1
                |> Option.map MateriaAliasMapper.TryMap
                |> Option.flatten

            if ret.IsNone then
                let tt = MateriaAliasMapper.GetValueTable()
                cmdArg.Reply(tt, PreferImage)
                cmdArg.Abort(InputError, "请按以下方案选择合适的魔晶石类别")
            else
                let key = ret.Value

                for grade in MateriaGrades do
                    acc.Update(itemCol.TryGetByName($"%s{key}魔晶石%s{grade}").Value)

            ()

        if acc.Count * worlds.Length >= 50 then
            cmdArg.Abort(InputError, "查询数量超过上线")

        TextTable(opt.ResponseType) {
            // 表头
            [ CellBuilder() { literal "物品" }
              CellBuilder() { literal "土豆" }
              CellBuilder() {
                  literal "数量"
                  rightAlign
              }
              CellBuilder() {
                  literal "总体出售"
                  rightAlign
              }
              CellBuilder() {
                  literal "HQ出售"
                  rightAlign
              }
              CellBuilder() {
                  literal "总体交易"
                  rightAlign
              }
              CellBuilder() {
                  literal "HQ交易"
                  rightAlign
              }
              CellBuilder() {
                  literal "更新时间"
                  rightAlign
              } ]

            let mutable sumListingAll, sumListingHq = 0.0, 0.0
            let mutable sumTradeAll, sumTradeHq = 0.0, 0.0

            // 每个物品*服务器单列
            [

              for mr in acc do
                  for world in worlds do
                      let uni =
                          universalis.GetMarketInfo(world, mr.Item)

                      let tradelog = uni.GetTradeLogAnalyzer()
                      let listing = uni.GetListingAnalyzer()
                      let mutable updated = TimeSpan.MaxValue

                      let lstAll =
                          listing.TakeVolume(25).StdEvPrice().Average
                          * mr.Quantity

                      let lstHq =
                          listing.TakeHQ().TakeVolume(25).StdEvPrice()
                              .Average
                          * mr.Quantity

                      updated <- min updated (listing.LastUpdateTime())
                      sumListingAll <- sumListingAll + lstAll
                      sumListingHq <- sumListingHq + lstHq

                      let logAll =
                          tradelog.StdEvPrice().Average * mr.Quantity

                      let logHq =
                          tradelog.TakeHQ().StdEvPrice().Average
                          * mr.Quantity

                      updated <- min updated (tradelog.LastUpdateTime())
                      sumTradeAll <- sumTradeAll + logAll
                      sumTradeHq <- sumTradeHq + logHq

                      [ CellBuilder() { literal mr.Item.Name }
                        CellBuilder() { literal world.WorldName }
                        CellBuilder() { integer mr.Quantity }
                        CellBuilder() { integer lstAll }
                        CellBuilder() { integer lstHq }
                        CellBuilder() { integer logAll }
                        CellBuilder() { integer logHq }
                        if updated = TimeSpan.MaxValue then
                            CellBuilder() { rightPad }
                        else
                            CellBuilder() { timeSpan updated } ] ]
            // 合计行
            [ if worlds.Length = 1 && acc.Count >= 2 then
                  [ CellBuilder() { literal "合计" }
                    CellBuilder() { leftPad }
                    CellBuilder() { rightPad }
                    CellBuilder() { integer sumListingAll }
                    CellBuilder() { integer sumListingHq }
                    CellBuilder() { integer sumTradeAll }
                    CellBuilder() { integer sumTradeHq }
                    CellBuilder() { rightPad } ] ]

        }
