﻿namespace KPX.TheBot.Module.XivModule.MarketModule

open System

open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable
open KPX.FsCqHttp.Testing

open KPX.TheBot.Data.CommonModule.Recipe
open KPX.TheBot.Data.XivData
open KPX.TheBot.Data.XivData.Shops

open KPX.TheBot.Utils.RecipeRPN

open KPX.TheBot.Module.XivModule.Utils
open KPX.TheBot.Module.XivModule.Utils.MarketUtils


type XivMarketModule() =
    inherit CommandHandlerBase()

    let rm = Recipe.XivRecipeManager.Instance
    let itemCol = ItemCollection.Instance
    let gilShop = GilShopCollection.Instance
    let xivExpr = XivExpression.XivExpression()

    let universalis =
        UniversalisMarketCache.MarketInfoCollection.Instance

    let isNumber (str : string) =
        if str.Length <> 0 then
            String.forall Char.IsDigit str
        else
            false

    let strToItem (str : string) =
        if isNumber str then
            itemCol.TryGetByItemId(Convert.ToInt32(str))
        else
            itemCol.TryGetByName(str.TrimEnd(CommandUtils.XivSpecialChars))

    /// 给物品名备注上NPC价格
    let tryLookupNpcPrice (item : XivItem) =
        let ret = gilShop.TryLookupByItem(item)

        if ret.IsSome then
            $"%s{item.Name}(%i{ret.Value.Ask})"
        else
            item.Name

    [<CommandHandlerMethod("#ffsrv", "检查Bot可用的FF14服务器/大区名称", "")>]
    member x.HandleFFCmdHelp(cmdArg : CommandEventArgs) =
        use ret = cmdArg.OpenResponse(PreferImage)

        let colsNum = 5 // 总列数

        let mkTable (strs : seq<string>) =
            let append =
                let c = strs |> Seq.length
                let padLen = (c % colsNum)

                if padLen = 0 then
                    Seq.empty<obj>
                else
                    let obj = box String.Empty
                    Seq.init (colsNum - padLen) (fun _ -> obj)

            let rows =
                append
                |> Seq.append (strs |> Seq.map box)
                |> Seq.chunkBySize colsNum
                |> Seq.toArray

            let tt = TextTable(rows |> Array.head)

            if rows.Length > 1 then
                for row in rows |> Array.tail do
                    tt.AddRow(row)

            tt


        let mainTable = mkTable World.WorldNames
        mainTable.AddPreTable("可用大区及缩写有：")
        mainTable.AddPreTable(mkTable World.DataCenterNames)
        mainTable.AddPreTable(" ")
        mainTable.AddPreTable("可用服务器及缩写有：")

        ret.Write(mainTable)

    [<TestFixture>]
    member x.TestFFSrv() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#ffsrv")

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

        let tt =
            TextTable(
                LeftAlignCell "物品",
                LeftAlignCell "土豆",
                RightAlignCell "数量",
                RightAlignCell "总体出售",
                RightAlignCell "HQ出售",
                RightAlignCell "总体交易",
                RightAlignCell "HQ交易",
                RightAlignCell "更新时间"
            )

        let acc = XivExpression.ItemAccumulator()

        let worlds = opt.World.Values

        match opt.NonOptionStrings |> Seq.tryHead with
        | None -> cmdArg.Reply("物品名或采集重建/魔晶石/水晶。")
        | Some "水晶" ->
            if worlds.Length >= 2 then
                cmdArg.Abort(InputError, "该选项不支持多服务器")

            [ 2 .. 19 ]
            |> Seq.iter
                (fun id ->
                    let item = itemCol.GetByItemId(id)
                    acc.Update(item))
        | Some "魔晶石" ->
            let ret =
                opt.NonOptionStrings
                |> Seq.tryItem 1
                |> Option.map MateriaAliasMapper.TryMap
                |> Option.flatten

            if ret.IsNone then
                let tt =
                    MateriaAliasMapper.GetValueTable()

                tt.AddPreTable("请按以下方案选择合适的魔晶石类别")
                using (cmdArg.OpenResponse(ForceImage)) (fun ret -> ret.Write(tt))
                cmdArg.Abort(IgnoreError, "")
            else
                let key = ret.Value

                for grade in MateriaGrades do
                    acc.Update(
                        itemCol
                            .TryGetByName($"%s{key}魔晶石%s{grade}")
                            .Value
                    )
        | Some "重建采集" ->
            if worlds.Length >= 2 then
                cmdArg.Abort(InputError, "该选项不支持多服务器")

            [ 31252 .. 31275 ]
            |> Seq.iter
                (fun id ->
                    let item = itemCol.GetByItemId(id)
                    acc.Update(item))
        | Some _ ->
            for str in opt.NonOptionStrings do
                match xivExpr.TryEval(str) with
                | Error err -> raise err
                | Ok (Number i) -> tt.AddPreTable $"计算结果为数字%f{i}，物品Id请加#"
                | Ok (Accumulator a) ->
                    for i in a do
                        acc.Update(i)

        if acc.Count * worlds.Length >= 50 then
            cmdArg.Abort(InputError, "查询数量超过上线")

        let mutable sumListingAll, sumListingHq = 0.0, 0.0
        let mutable sumTradeAll, sumTradeHq = 0.0, 0.0

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

                tt.RowBuilder {
                    yield mr.Item.Name
                    yield world.WorldName
                    yield HumanReadableInteger mr.Quantity
                    yield HumanReadableInteger lstAll
                    yield HumanReadableInteger lstHq
                    yield HumanReadableInteger logAll
                    yield HumanReadableInteger logHq

                    if updated = TimeSpan.MaxValue then
                        yield PaddingRight
                    else
                        yield HumanTimeSpan updated
                }
                |> tt.AddRow

        if worlds.Length = 1 && acc.Count >= 2 then
            tt.RowBuilder {
                yield "合计"
                yield "--"
                yield PaddingRight
                yield HumanReadableInteger sumListingAll
                yield HumanReadableInteger sumListingHq
                yield HumanReadableInteger sumTradeAll
                yield HumanReadableInteger sumTradeHq
                yield PaddingRight
            }
            |> tt.AddRow

        using (cmdArg.OpenResponse(opt.ResponseType)) (fun ret -> ret.Write(tt))

    [<TestFixture>]
    member x.TestXivMarket() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#fm 风之水晶")
        tc.ShouldNotThrow("#fm 风之水晶 拉诺西亚")
        tc.ShouldNotThrow("#fm 风之水晶 一区")

    [<CommandHandlerMethod("#r", "根据表达式汇总多个物品的材料，不查询价格", "可以使用text:选项返回文本。如#r 白钢锭 text:")>]
    [<CommandHandlerMethod("#rr",
                                    "根据表达式汇总多个物品的基础材料，不查询价格",
                                    "可以使用text:选项返回文本。如#rr 白钢锭 text:")>]
    [<CommandHandlerMethod("#rc",
                                    "计算物品基础材料成本",
                                    "可以使用text:选项返回文本。
可以设置查询服务器，已有服务器见#ff14help")>]
    [<CommandHandlerMethod("#rrc",
                                    "计算物品基础材料成本",
                                    "可以使用text:选项返回文本。
可以设置查询服务器，已有服务器见#ff14help")>]
    member _.GeneralRecipeCalculator(cmdArg : CommandEventArgs) =
        let doCalculateCost =
            cmdArg.CommandName = "#rrc"
            || cmdArg.CommandName = "#rc"

        let materialFunc =
            if cmdArg.CommandName = "#rr"
               || cmdArg.CommandName = "#rrc" then
                fun (item : XivItem) -> rm.TryGetRecipeRec(item, ByItem 1.0)
            else
                fun (item : XivItem) -> rm.TryGetRecipe(item)

        let opt = CommandUtils.XivOption()
        opt.Parse(cmdArg.HeaderArgs)

        let world = opt.World.Value

        let tt =
            if doCalculateCost then
                TextTable(
                    "物品",
                    RightAlignCell "价格",
                    RightAlignCell "数量",
                    RightAlignCell "小计",
                    RightAlignCell "更新时间"
                )
            else
                TextTable("物品", RightAlignCell "数量")

        if doCalculateCost then
            tt.AddPreTable $"服务器：%s{world.WorldName}"

        let product = XivExpression.ItemAccumulator()
        let acc = XivExpression.ItemAccumulator()

        for str in opt.NonOptionStrings do
            match xivExpr.TryEval(str) with
            | Error err -> raise err
            | Ok (Number i) -> cmdArg.Abort(InputError, "计算结果为数字{0}，物品Id请加#", i)
            | Ok (Accumulator a) ->
                for mr in a do
                    product.Update(mr)
                    let recipe = materialFunc mr.Item // 一个物品的材料

                    if recipe.IsNone then
                        tt.AddPreTable $"%s{mr.Item.Name} 没有生产配方"
                    else
                        for m in recipe.Value.Input do
                            acc.Update(m.Item, m.Quantity * mr.Quantity)

        if acc.Count = 0 then
            cmdArg.Abort(InputError, "缺少表达式")

        let mutable sum = StdEv.Zero

        for mr in acc |> Seq.sortBy (fun kv -> kv.Item.Id) do
            let market =
                if doCalculateCost then
                    universalis
                        .GetMarketInfo(world, mr.Item)
                        .GetListingAnalyzer()
                        .TakeVolume()
                    |> Some
                else
                    None

            tt.RowBuilder {
                yield mr.Item |> tryLookupNpcPrice

                if doCalculateCost then
                    yield HumanReadableInteger(market.Value.StdEvPrice().Average)

                yield HumanReadableInteger mr.Quantity

                if doCalculateCost then
                    let subtotal = market.Value.StdEvPrice() * mr.Quantity

                    sum <- sum + subtotal
                    yield HumanReadableInteger subtotal.Average
                    yield HumanTimeSpan(market.Value.LastUpdateTime())
            }
            |> tt.AddRow

        if doCalculateCost then
            tt.AddRowFill("成本总计", PaddingRight, PaddingRight, HumanReadableInteger sum.Average)

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

            tt.AddRowFill(
                "卖出价格",
                PaddingRight,
                PaddingRight,
                HumanReadableInteger totalSell.Average
            )

            let profit = (totalSell - sum).Average
            tt.AddRowFill("税前利润", PaddingRight, PaddingRight, HumanReadableInteger profit)

        using (cmdArg.OpenResponse(opt.ResponseType)) (fun x -> x.Write(tt))

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

    [<CommandHandlerMethod("#ssc",
                                    "计算部分道具兑换的价格",
                                    "兑换所需道具的名称或ID，只处理1个
可以设置查询服务器，已有服务器见#ff14help")>]
    member x.HandleSSC(cmdArg : CommandEventArgs) =
        let sc = SpecialShopCollection.Instance

        let opt = CommandUtils.XivOption()
        opt.Parse(cmdArg.HeaderArgs)
        let world = opt.World.Value

        if opt.NonOptionStrings.Count = 0 then
            //回复所有可交易道具
            let headerSet =
                [| box <| RightAlignCell "ID"
                   box <| LeftAlignCell "名称" |]

            let headerCol = 3

            let header =
                [| for _ = 0 to headerCol - 1 do
                       yield! headerSet |]

            let tt = TextTable(header)
            tt.AddPreTable("可交换道具：")

            let chunks =
                sc.AllCostItems()
                |> Seq.sortBy (fun item -> item.Id)
                |> Seq.chunkBySize headerCol

            for chunk in chunks do
                tt.RowBuilder {
                    for item in chunk do
                        yield item.Id
                        yield item.Name

                    for _ = 0 to headerCol - chunk.Length - 1 do
                        yield PaddingRight
                        yield PaddingLeft
                }
                |> tt.AddRow

            using (cmdArg.OpenResponse(ForceImage)) (fun x -> x.Write(tt))
        else
            let ret = strToItem opt.NonOptionStrings.[0]

            match ret with
            | None -> cmdArg.Abort(ModuleError, "找不到物品{0}", opt.NonOptionStrings.[0])
            | Some reqi ->
                let ia = sc.SearchByCostItemId(reqi.Id)

                if ia.Length = 0 then
                    cmdArg.Abort(InputError, "{0} 不能兑换道具", reqi.Name)

                let tt =
                    TextTable(
                        "兑换物品",
                        RightAlignCell "价格",
                        RightAlignCell "最低",
                        RightAlignCell "兑换价值",
                        RightAlignCell "更新时间"
                    )

                tt.AddPreTable $"兑换道具:%s{reqi.Name} 土豆：%s{world.DataCenter}/%s{world.WorldName}"

                ia
                |> Array.map
                    (fun info ->
                        let recv = itemCol.GetByItemId(info.ReceiveItem)

                        let market =
                            universalis
                                .GetMarketInfo(world, recv)
                                .GetListingAnalyzer()
                                .TakeVolume()

                        let updated = market.LastUpdateTime()

                        (updated,
                         tt.RowBuilder {
                             yield recv.Name
                             yield HumanReadableInteger(market.StdEvPrice().Average)
                             yield HumanReadableInteger(market.MinPrice())

                             yield
                                 HumanReadableInteger(
                                     (market.StdEvPrice() * (float <| info.ReceiveCount)
                                      / (float <| info.CostCount))
                                         .Average
                                 )

                             if market.IsEmpty then
                                 yield PaddingRight
                             else
                                 yield HumanTimeSpan updated

                         }))
                |> Array.sortBy fst
                |> Array.iter (fun (_, row) -> tt.AddRow(row))

                using (cmdArg.OpenResponse(ForceImage)) (fun x -> x.Write(tt))

    [<TestFixture>]
    member x.TestXivSSC() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#ssc")
        tc.ShouldNotThrow("#ssc 盐酸")

    [<CommandHandlerMethod("#理符",
                                    "计算制作理符利润（只查询70级以上的基础材料）",
                                    "#理符 [职业名] [服务器名]",
                                    Disabled = true)>]
    member x.HandleCraftLeve(cmdArg : CommandEventArgs) =
        let opt = CommandUtils.XivOption()
        opt.Parse(cmdArg.HeaderArgs)

        let leves =
            opt.NonOptionStrings
            |> Seq.tryHead
            |> Option.map
                ClassJobMapping.ClassJobMappingCollection.Instance.TrySearchByName
            |> Option.flatten
            |> Option.map CraftLeve.CraftLeveInfoCollection.Instance.GetByClassJob

        if leves.IsNone then
            cmdArg.Abort(InputError, "未设置职业或职业无效")

        let leves =
            leves.Value
            |> Array.filter (fun leve -> leve.Level >= 60)
            |> Array.sortBy (fun leve -> leve.Level)

        //let tt =TextTable("名称", "等级", "制作价格", "金币奖励", "利润率", "最旧更新")

        for leve in leves do
            for item in leve.Items do
                let quantity = ByItem item.Quantity
                let item = itemCol.GetByItemId(item.Item)
                // 生产理符都能搓
                let materials = rm.TryGetRecipeRec(item, quantity).Value
                materials |> ignore //屏蔽警告用
                ()

            ()

    [<TestFixture>]
    member x.TestXivLeveCraft() = ()