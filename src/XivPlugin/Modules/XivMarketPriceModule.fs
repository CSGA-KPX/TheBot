namespace KPX.XivPlugin.Modules.XivMarketPrice

open System

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.Subcommands
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Testing

open KPX.TheBot.Host.Utils.RecipeRPN

open KPX.XivPlugin.Data
open KPX.XivPlugin.Modules.Utils
open KPX.XivPlugin.Modules.Utils.MarketUtils


type MarketSubcommands =
    | [<AltCommandName("水晶", "碎晶", "晶簇")>] Crystals
    | [<AltCommandName("魔晶石")>] Materia

    interface ISubcommandTemplate with
        member x.Usage =
            match x with
            | Crystals -> "水晶"
            | Materia -> "魔晶石"


type XivMarketPriceModule() =
    inherit CommandHandlerBase()

    let itemCol = ItemCollection.Instance
    let xivExpr = XivExpression.XivExpression()
    let universalis = MarketInfoCollection.Instance

    [<TestFixture>]
    member x.TestXivMarket() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#fm 风之水晶")
        tc.ShouldNotThrow("#fm 风之水晶 拉诺西亚")
        tc.ShouldNotThrow("#fm 风之水晶 一区")

    [<CommandHandlerMethod("#fm",
                           "FF14市场查询。可以使用 魔晶石/水晶 快捷组",
                           "接受以下参数：
text 以文本格式输出结果
分区/服务器名 调整查询分区下的所有服务器。见#ff14help
#fm 一区 风之水晶 text:
#fm 拉诺 紫水 风之水晶")>]
    member x.HandleXivMarket(cmdArg: CommandEventArgs) =
        let opt = CommandUtils.XivOption()
        opt.Parse(cmdArg.HeaderArgs)

        let acc = XivExpression.ItemAccumulator()
        let worlds = opt.World.Values |> Array.sortBy (fun w -> w.WorldName)

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

        if acc.Count * worlds.Length >= 50 then
            cmdArg.Abort(InputError, "查询数量超过上线")

        let canHq = acc |> Seq.exists (fun mr -> mr.Item.CanHq)

        TextTable(opt.ResponseType) {
            // 表头
            AsCols [ Literal "物品"
                     Literal "土豆"
                     RLiteral "数量"
                     RLiteral "总体出售"
                     if canHq then RLiteral "HQ出售"
                     RLiteral "总体交易"
                     if canHq then RLiteral "HQ交易"
                     RLiteral "更新时间" ]

            let mutable sumListingAll, sumListingHq = 0.0, 0.0
            let mutable sumTradeAll, sumTradeHq = 0.0, 0.0

            // 每个物品*服务器单列
            [

              for mr in acc do
                  for world in worlds do
                      let uni = universalis.GetMarketInfo(world, mr.Item)

                      let updated = uni.LastUpdated

                      let lstAll = uni.ListingAllSampledPrice() * mr.Quantity
                      let lstHq = uni.ListingHqSampledPrice() * mr.Quantity
                      let logAll = uni.TradelogAllPrice() * mr.Quantity
                      let logHq = uni.TradeLogHqPrice() * mr.Quantity

                      sumListingAll <- sumListingAll + lstAll
                      sumListingHq <- sumListingHq + lstHq
                      sumTradeAll <- sumTradeAll + logAll
                      sumTradeHq <- sumTradeHq + logHq

                      [ Literal mr.Item.DisplayName
                        Literal world.WorldName
                        Integer mr.Quantity
                        Integer lstAll
                        if canHq then Integer lstHq
                        Integer logAll
                        if canHq then Integer logHq
                        TimeSpan updated ] ]
            // 合计行
            [ if worlds.Length = 1 && acc.Count >= 2 then
                  [ Literal "合计"
                    LeftPad
                    RightPad
                    Integer sumListingAll
                    if canHq then Integer sumListingHq
                    Integer sumTradeAll
                    if canHq then Integer sumTradeHq
                    RightPad ] ]
        }
