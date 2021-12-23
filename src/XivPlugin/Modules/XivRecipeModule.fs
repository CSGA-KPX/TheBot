namespace KPX.XivPlugin.Modules.XivRecipe

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Testing

open KPX.TheBot.Host.DataModel.Recipe
open KPX.TheBot.Host.Utils.RecipeRPN

open KPX.XivPlugin.Data
open KPX.XivPlugin.Data.Shop
open KPX.XivPlugin.Modules.Utils
open KPX.XivPlugin.Modules.Utils.MarketUtils


type XivRecipeModule() =
    inherit CommandHandlerBase()

    let getRm (r: VersionRegion) = XivRecipeManager.GetInstance r
    let gilShop = GilShopCollection.Instance
    let xivExpr = XivExpression.XivExpression()
    let universalis = MarketInfoCollection.Instance

    /// 给物品名备注上NPC价格
    let tryLookupNpcPrice (item: XivItem, world: World) =
        let ret = gilShop.TryLookupByItem(item, world.VersionRegion)

        if ret.IsSome then
            $"%s{item.DisplayName}(%i{ret.Value.AskPrice})"
        else
            item.DisplayName

    [<CommandHandlerMethod("#r", "根据表达式汇总多个物品的材料，不查询价格", "可以使用text:选项返回文本。如#r 白钢锭 text:")>]
    [<CommandHandlerMethod("#rr", "根据表达式汇总多个物品的基础材料，不查询价格", "可以使用text:选项返回文本。如#rr 白钢锭 text:")>]
    [<CommandHandlerMethod("#rc",
                           "计算物品基础材料成本",
                           "可以使用text:选项返回文本。
可以设置查询服务器，已有服务器见#ff14help")>]
    [<CommandHandlerMethod("#rrc",
                           "计算物品基础材料成本",
                           "可以使用text:选项返回文本。
可以设置查询服务器，已有服务器见#ff14help")>]
    member _.GeneralRecipeCalculator(cmdArg: CommandEventArgs) =
        let doCalculateCost = cmdArg.CommandName = "#rrc" || cmdArg.CommandName = "#rc"

        let opt = CommandUtils.XivOption()
        opt.Parse(cmdArg.HeaderArgs)

        let world = opt.World.Value
        let mutable region = world.VersionRegion

        let getMaterialFunc region =
            let rm = getRm region

            if cmdArg.CommandName = "#rr" || cmdArg.CommandName = "#rrc" then
                fun (item: XivItem) -> rm.TryGetRecipeRec(item, ByItem 1.0)
            else
                fun (item: XivItem) -> rm.TryGetRecipe(item)

        let product = XivExpression.ItemAccumulator()
        let acc = XivExpression.ItemAccumulator()

        for str in opt.NonOptionStrings do
            match xivExpr.TryEval(str) with
            | Error err -> raise err
            | Ok (Number i) -> cmdArg.Abort(InputError, "计算结果为数字{0}，物品Id请加#", i)
            | Ok (Accumulator a) ->
                // 如果物品里面有国服名称为空（即国际服才有的物品）
                // 则覆盖区域到世界服
                for mr in a do
                    if System.String.IsNullOrWhiteSpace(mr.Item.ChineseName) then
                        region <- VersionRegion.Offical

                let materialFunc = getMaterialFunc (region)

                for mr in a do
                    product.Update(mr)
                    let recipe = materialFunc mr.Item // 一个物品的材料

                    if recipe.IsNone then
                        cmdArg.Abort(InputError, $"%s{mr.Item.DisplayName} 没有生产配方")
                    else
                        for m in recipe.Value.Input do
                            acc.Update(m.Item, m.Quantity * mr.Quantity)

        if acc.Count = 0 then
            cmdArg.Abort(InputError, "缺少表达式")

        TextTable(opt.ResponseType) {
            if doCalculateCost then
                $"土豆：%s{world.WorldName}"

                if world.VersionRegion <> region then
                    $"警告：服务器版本[{world.VersionRegion}]和物品版本[{region}]不符"
                    $"如非预期结果，请写明想要查询的服务器"

            // 表头
            [ CellBuilder() { literal "物品" }
              CellBuilder() { literal "数量" }
              if doCalculateCost then
                  CellBuilder() {
                      literal "价格"
                      rightAlign
                  }

                  CellBuilder() {
                      literal "小计"
                      rightAlign
                  }

                  CellBuilder() {
                      literal "更新"
                      rightAlign
                  } ]

            // 单项列
            let mutable sum = StdEv.Zero

            [ for mr in acc |> Seq.sortBy (fun kv -> kv.Item.ItemId) do
                  let market =
                      lazy
                          (universalis
                              .GetMarketInfo(world, mr.Item)
                              .GetListingAnalyzer()
                              .TakeVolume())

                  [ CellBuilder() { literal (tryLookupNpcPrice (mr.Item, world)) }
                    CellBuilder() { integer mr.Quantity }
                    if doCalculateCost then
                        let stdPrice = market.Value.StdEvPrice()
                        CellBuilder() { integer stdPrice.Average }
                        let subtotal = stdPrice * mr.Quantity
                        sum <- sum + subtotal
                        CellBuilder() { integer subtotal.Average }
                        CellBuilder() { timeSpan (market.Value.LastUpdateTime()) } ] ]

            [ if doCalculateCost then
                  [ CellBuilder() { literal "成本总计" }
                    CellBuilder() { rightPad }
                    CellBuilder() { rightPad }
                    CellBuilder() { integer sum.Average } ]

                  let totalSell =
                      product
                      |> Seq.sumBy
                          (fun mr ->
                              let lst =
                                  universalis
                                      .GetMarketInfo(world, mr.Item)
                                      .GetListingAnalyzer()
                                      .TakeVolume()

                              lst.StdEvPrice() * mr.Quantity)

                  [ CellBuilder() { literal "卖出价格" }
                    CellBuilder() { rightPad }
                    CellBuilder() { rightPad }
                    CellBuilder() { integer totalSell.Average } ]

                  [ CellBuilder() { literal "税前利润" }
                    CellBuilder() { rightPad }
                    CellBuilder() { rightPad }
                    CellBuilder() { integer (totalSell - sum).Average } ] ]

        }

    [<TestFixture>]
    member x.TestXivRecipe() =
        let tc = TestContext(x)
        // 数据正确与否在BotData的单元测试中进行

        // 空值
        tc.ShouldThrow("#r")
        tc.ShouldThrow("#rr")
        tc.ShouldThrow("#rc")
        tc.ShouldThrow("#rrc")

        // 不存在值
        tc.ShouldThrow("#r 不存在物品")
        tc.ShouldThrow("#rr 不存在物品")
        tc.ShouldThrow("#rc 不存在物品")
        tc.ShouldThrow("#rrc 不存在物品")

        // 纯数字计算
        tc.ShouldThrow("#r 5*5")
        tc.ShouldThrow("#rr 5*5")
        tc.ShouldThrow("#rc 5*5")
        tc.ShouldThrow("#rrc 5*5")

        // 常规道具计算
        tc.ShouldNotThrow("#r 亚拉戈高位合成兽革")
        tc.ShouldNotThrow("#r 亚拉戈高位合成兽革*555")
        tc.ShouldNotThrow("#r 亚拉戈高位合成兽革*(5+10)")
        tc.ShouldNotThrow("#rr 亚拉戈高位合成兽革")
        tc.ShouldNotThrow("#rr 亚拉戈高位合成兽革*555")
        tc.ShouldNotThrow("#rr 亚拉戈高位合成兽革*(5+10)")
        tc.ShouldNotThrow("#rc 亚拉戈高位合成兽革")
        tc.ShouldNotThrow("#rrc 亚拉戈高位合成兽革")
        tc.ShouldNotThrow("#rc 亚拉戈高位合成兽革 拉诺西亚")
        tc.ShouldNotThrow("#rrc 亚拉戈高位合成兽革 拉诺西亚")

        // 部队工坊
        tc.ShouldNotThrow("#r 野马级船体")
        tc.ShouldNotThrow("#r 野马级船体*555")
        tc.ShouldNotThrow("#r 野马级船体*(5+10)")
        tc.ShouldNotThrow("#rr 野马级船体")
        tc.ShouldNotThrow("#rr 野马级船体*555")
        tc.ShouldNotThrow("#rr 野马级船体*(5+10)")
        tc.ShouldNotThrow("#rc 野马级船体")
        tc.ShouldNotThrow("#rrc 野马级船体")
        tc.ShouldNotThrow("#rc 野马级船体 拉诺西亚")
        tc.ShouldNotThrow("#rrc 野马级船体 拉诺西亚")
