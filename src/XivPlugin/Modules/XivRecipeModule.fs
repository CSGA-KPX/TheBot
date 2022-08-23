namespace KPX.XivPlugin.Modules.XivRecipe

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Testing

open KPX.TheBot.Host.DataModel.Recipe
open KPX.TheBot.Host.Utils.RecipeRPN

open KPX.XivPlugin.Data
open KPX.XivPlugin.Data.Shop
open KPX.XivPlugin.Modules.Utils


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

    [<CommandHandlerMethod("#r", "FF14:根据表达式汇总多个物品的材料，不查询价格", "可以使用text:选项返回文本。如#r 白钢锭 text:")>]
    [<CommandHandlerMethod("#rr", "FF14:根据表达式汇总多个物品的基础材料，不查询价格", "可以使用text:选项返回文本。如#rr 白钢锭 text:")>]
    member _.RecipeMaterialCalculator(cmdArg: CommandEventArgs) =
        let opt = CommandUtils.XivOption()
        opt.Parse(cmdArg.HeaderArgs)

        let world = opt.World.Value
        let mutable region = world.VersionRegion

        let getMaterialFunc region =
            let rm = getRm region

            if cmdArg.CommandName = "#rr" then
                fun (item: XivItem) -> rm.TryGetRecipeRec(item, ByItem 1.0)
            else
                fun (item: XivItem) -> rm.TryGetRecipe(item, ByItem 1.0)

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
            // 表头
            AsCols [ Literal "物品"; RLiteral "数量" ]

            Literal "材料：" { bold }

            [ for mr in acc |> Seq.sortBy (fun kv -> kv.Item.ItemId) do
                  [ Literal(tryLookupNpcPrice (mr.Item, world)); CellUtils.Number mr.Quantity ] ]

            Literal "产出：" { bold }

            [ for mr in product do
                  [ Literal mr.Item.DisplayName; CellUtils.Number mr.Quantity ] ]
        }

    [<CommandHandlerMethod("#rc", "FF14:根据表达式汇总多个物品的材料，不查询价格", "可以使用text:选项返回文本。如#r 白钢锭 text:")>]
    [<CommandHandlerMethod("#rrc", "FF14:根据表达式汇总多个物品的材料，不查询价格", "", IsHidden = true)>]
    member _.RecipeProfitCalculator(cmdArg: CommandEventArgs) =
        let opt = CommandUtils.XivOption()
        opt.Parse(cmdArg.HeaderArgs)

        let world = opt.World.Value
        let mutable region = world.VersionRegion

        let input = XivExpression.ItemAccumulator()
        let output = XivExpression.ItemAccumulator()

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

                // 需要先确定区域，不能合并到同一个循环
                for mr in a do
                    output.Update(mr)
                    let directMaterial = (getRm region).TryGetRecipe(mr)

                    if directMaterial.IsNone then
                        cmdArg.Abort(InputError, $"%s{mr.Item.DisplayName} 没有生产配方")
                    else
                        for m in directMaterial.Value.Input do
                            input.Update(m)

        if input.Count = 0 then
            cmdArg.Abort(InputError, "缺少表达式")

        TextTable(opt.ResponseType) {
            $"土豆：%s{world.WorldName}"

            if world.VersionRegion <> region then
                $"警告：服务器版本[{world.VersionRegion}]和物品版本[{region}]不符"
                $"如非预期结果，请写明想要查询的服务器"

            let mutable totalSell = 0.0

            Literal "产出：" { bold }

            AsCols [ Literal "物品"
                     Literal "数量"
                     RLiteral "税前"
                     RLiteral "小计"
                     RLiteral "更新" ]

            [ for mr in output do
                  [ Literal mr.Item.DisplayName
                    CellUtils.Number mr.Quantity
                    let uni = universalis.GetMarketInfo(world, mr.Item)
                    let unitPrice = uni.AllPrice()
                    let subTotal = unitPrice * mr.Quantity
                    totalSell <- totalSell + subTotal

                    HumanSig4 unitPrice
                    HumanSig4 subTotal
                    TimeSpan uni.LastUpdated ] ]

            let mutable inputBuySum = 0.0
            let mutable inputCraftSum = 0.0
            let mutable inputOptSum = 0.0

            Literal "材料：" { bold }

            AsCols [ Literal "物品"
                     RLiteral "数量"
                     RLiteral "税前"
                     RLiteral "小计"
                     RLiteral "制作" ]

            [ for mr in input do

                  let inline func (m: RecipeMaterial<_>) =
                      universalis
                          .GetMarketInfo(world, m.Item)
                          .AllPrice()
                      * m.Quantity

                  let sellPrice = func mr
                  let unitPrice = sellPrice / mr.Quantity
                  let mutable preferSell = true

                  let craftPrice =
                      (getRm region).TryGetRecipeRec(mr)
                      |> Option.map (fun proc -> proc.Input |> Array.Parallel.map func |> Array.sum)

                  if craftPrice.IsNone then
                      inputBuySum <- inputBuySum + sellPrice
                      inputCraftSum <- inputCraftSum + sellPrice
                      inputOptSum <- inputOptSum + sellPrice
                      preferSell <- true
                  else if sellPrice <= craftPrice.Value then
                      inputBuySum <- inputBuySum + sellPrice
                      inputCraftSum <- inputCraftSum + craftPrice.Value
                      inputOptSum <- inputOptSum + sellPrice
                      preferSell <- true
                  else
                      inputBuySum <- inputBuySum + sellPrice
                      inputCraftSum <- inputCraftSum + craftPrice.Value
                      inputOptSum <- inputOptSum + craftPrice.Value
                      preferSell <- false

                  [ Literal(tryLookupNpcPrice (mr.Item, world))
                    CellUtils.Number mr.Quantity
                    HumanSig4 unitPrice
                    HumanSig4 sellPrice { boldIf preferSell }
                    if craftPrice.IsNone then
                        RightPad
                    else
                        HumanSig4(craftPrice.Value) { boldIf (not preferSell) } ] ]

            let taxBuyRate = 1.05
            let taxSellRate = 0.95

            // 材料小计
            AsCols [ Literal "税后卖出" { bold }
                     RightPad
                     HumanSig4(totalSell * taxSellRate) ]

            AsCols [ Literal "税后材料" { bold }
                     RightPad
                     HumanSig4(inputBuySum * taxBuyRate)
                     HumanSig4(inputCraftSum * taxBuyRate) ]

            AsCols [ Literal "最优成本/利润" { bold }
                     RightPad
                     HumanSig4(inputOptSum * taxBuyRate)
                     HumanSig4((totalSell * taxSellRate) - (inputOptSum * taxBuyRate)) ]
        }

    [<TestFixture>]
    member x.TestXivRecipe() =
        let tc = TestContext(x)
        // 数据正确与否在BotData的单元测试中进行

        // 空值
        tc.ShouldThrow("#r")
        tc.ShouldThrow("#rr")
        // 不存在值
        tc.ShouldThrow("#r 不存在物品")
        tc.ShouldThrow("#rr 不存在物品")
        // 纯数字计算
        tc.ShouldThrow("#r 5*5")
        tc.ShouldThrow("#rr 5*5")
        // 常规道具计算
        tc.ShouldNotThrow("#r 亚拉戈高位合成兽革")
        tc.ShouldNotThrow("#r 亚拉戈高位合成兽革*555")
        tc.ShouldNotThrow("#r 亚拉戈高位合成兽革*(5+10)")
        tc.ShouldNotThrow("#rr 亚拉戈高位合成兽革")
        tc.ShouldNotThrow("#rr 亚拉戈高位合成兽革*555")
        tc.ShouldNotThrow("#rr 亚拉戈高位合成兽革*(5+10)")
        // 部队工坊
        tc.ShouldNotThrow("#r 野马级船体")
        tc.ShouldNotThrow("#r 野马级船体*555")
        tc.ShouldNotThrow("#r 野马级船体*(5+10)")
        tc.ShouldNotThrow("#rr 野马级船体")
        tc.ShouldNotThrow("#rr 野马级船体*555")
        tc.ShouldNotThrow("#rr 野马级船体*(5+10)")
