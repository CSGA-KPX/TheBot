namespace KPX.TheBot.Module.XivMarketModule

open System

open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

open KPX.TheBot.Data.CommonModule.Recipe
open KPX.TheBot.Data.XivData
open KPX.TheBot.Data.XivData.Shops

open KPX.TheBot.Utils.Config
open KPX.TheBot.Utils.RecipeRPN

open KPX.TheBot.Module.XivModule.Utils
open KPX.TheBot.Module.XivModule.Utils.MarketUtils


type XivMarketModule() =
    inherit CommandHandlerBase()

    let rm = Recipe.XivRecipeManager.Instance
    let itemCol = ItemCollection.Instance
    let gilShop = GilShopCollection.Instance
    let xivExpr = XivExpression.XivExpression()
    let universalis = UniversalisMarketCache.MarketInfoCollection.Instance


    let isNumber (str : string) =
        if str.Length <> 0 then String.forall (Char.IsDigit) str else false

    let strToItem (str : string) =
        if isNumber (str)
        then itemCol.TryGetByItemId(Convert.ToInt32(str))
        else itemCol.TryGetByName(str.TrimEnd(CommandUtils.XivSpecialChars))

    /// 给物品名备注上NPC价格
    let tryLookupNpcPrice (item : XivItem) =
        let ret = gilShop.TryLookupByItem(item)
        if ret.IsSome then sprintf "%s(%i)" item.Name ret.Value.Ask else item.Name

    [<CommandHandlerMethodAttribute("#ffsrv", "设置默认查询的服务器", "")>]
    member x.HandleXivDefSrv(cmdArg : CommandEventArgs) =
        let cfg = CommandUtils.XivConfig(cmdArg)

        if cfg.IsWorldDefined then
            let cm =
                ConfigManager(ConfigOwner.User(cmdArg.MessageEvent.UserId))

            let world = cfg.GetWorld()
            cm.Put(CommandUtils.defaultServerKey, world)

            cmdArg.QuickMessageReply(
                sprintf "%s的默认服务器设置为%s" cmdArg.MessageEvent.DisplayName world.WorldName
            )
        else
            cmdArg.QuickMessageReply("没有指定服务器或服务器名称不正确")

    //[<CommandHandlerMethodAttribute("ffhelp", "FF14指令帮助", "")>]
    member x.HandleFFCmdHelp(cmdArg : CommandEventArgs) =
        use ret = cmdArg.OpenResponse(ForceText)
        ret.WriteLine("当前可使用服务器及缩写有：{0}", String.Join(" ", World.WorldNames))
        ret.WriteLine("当前可使用大区及缩写有：{0}", String.Join(" ", World.DataCenterNames))

    [<CommandHandlerMethodAttribute("#fm",
                                    "FF14市场查询。可以使用 采集重建/魔晶石/水晶 快捷组",
                                    "接受以下参数：
text 以文本格式输出结果
分区/服务器名 调整查询分区下的所有服务器。见#ff14help
#fm 一区 风之水晶 text:
#fm 拉诺 紫水 风之水晶")>]
    member x.HandleXivMarket(cmdArg : CommandEventArgs) =
        let cfg = CommandUtils.XivConfig(cmdArg)

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

        let worlds = ResizeArray<World>(cfg.GetWorlds())

        match cfg.CommandLine |> Array.tryHead with
        | None -> cmdArg.QuickMessageReply("物品名或采集重建/魔晶石/水晶。")
        | Some "水晶" ->
            if worlds.Count >= 2 then cmdArg.AbortExecution(InputError, "该选项不支持多服务器")

            [ 2 .. 19 ]
            |> Seq.iter
                (fun id ->
                    let item = itemCol.GetByItemId(id)
                    acc.Update(item))
        | Some "魔晶石" ->
            let ret =
                cfg.CommandLine
                |> Array.tryItem 1
                |> Option.map MarketUtils.MateriaAliasMapper.TryMap
                |> Option.flatten

            if ret.IsNone then
                let tt =
                    MarketUtils.MateriaAliasMapper.GetValueTable()

                tt.AddPreTable("请按以下方案选择合适的魔晶石类别")
                using (cmdArg.OpenResponse(ForceImage)) (fun ret -> ret.Write(tt))
                cmdArg.AbortExecution(IgnoreError, "")
            else
                let key = ret.Value

                for grade in MarketUtils.MateriaGrades do
                    acc.Update(
                        itemCol
                            .TryGetByName(sprintf "%s魔晶石%s" key grade)
                            .Value
                    )
        | Some "重建采集" ->
            if worlds.Count >= 2 then cmdArg.AbortExecution(InputError, "该选项不支持多服务器")

            [ 31252 .. 31275 ]
            |> Seq.iter
                (fun id ->
                    let item = itemCol.GetByItemId(id)
                    acc.Update(item))
        | Some _ ->
            for str in cfg.CommandLine do
                match xivExpr.TryEval(str) with
                | Error err -> raise err
                | Ok (Number i) -> tt.AddPreTable(sprintf "计算结果为数字%f，物品Id请加#" i)
                | Ok (Accumulator a) ->
                    for i in a do
                        acc.Update(i)

        if acc.Count * worlds.Capacity >= 50 then cmdArg.AbortExecution(InputError, "查询数量超过上线")

        let mutable sumListingAll, sumListingHq = 0.0, 0.0
        let mutable sumTradeAll, sumTradeHq = 0.0, 0.0

        for mr in acc do
            for world in worlds do
                let uni = universalis.GetMarketInfo(world, mr.Item)
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

        if worlds.Count = 1 && acc.Count >= 2 then
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

        using (cmdArg.OpenResponse(cfg.IsImageOutput)) (fun ret -> ret.Write(tt))

    [<CommandHandlerMethodAttribute("#r", "根据表达式汇总多个物品的材料，不查询价格", "可以使用text:选项返回文本。如#r 白钢锭 text:")>]
    [<CommandHandlerMethodAttribute("#rr", "根据表达式汇总多个物品的基础材料，不查询价格", "可以使用text:选项返回文本。如#rr 白钢锭 text:")>]
    [<CommandHandlerMethodAttribute("#rc",
                                    "计算物品基础材料成本",
                                    "可以使用text:选项返回文本。
可以设置查询服务器，已有服务器见#ff14help")>]
    [<CommandHandlerMethodAttribute("#rrc",
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

        let cfg = CommandUtils.XivConfig(cmdArg)
        let world = cfg.GetWorld()

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

        if doCalculateCost then tt.AddPreTable(sprintf "服务器：%s" world.WorldName)

        let product = XivExpression.ItemAccumulator()
        let acc = XivExpression.ItemAccumulator()

        for str in cfg.CommandLine do
            match xivExpr.TryEval(str) with
            | Error err -> raise err
            | Ok (Number i) -> tt.AddPreTable(sprintf "计算结果为数字%f，物品Id请加#" i)
            | Ok (Accumulator a) ->
                for mr in a do
                    product.Update(mr)
                    let recipe = materialFunc (mr.Item) // 一个物品的材料

                    if recipe.IsNone then
                        tt.AddPreTable(sprintf "%s 没有生产配方" mr.Item.Name)
                    else
                        for m in recipe.Value.Input do
                            acc.Update(m.Item, m.Quantity * mr.Quantity)

        let mutable sum = MarketUtils.StdEv.Zero

        for mr in acc |> Seq.sortBy (fun kv -> kv.Item.Id) do
            let market =
                if doCalculateCost then
                    universalis.GetMarketInfo(world, mr.Item)
                        .GetListingAnalyzer()
                        .TakeVolume()
                    |> Some
                else
                    None

            tt.RowBuilder {
                yield mr.Item |> tryLookupNpcPrice

                if doCalculateCost
                then yield HumanReadableInteger(market.Value.StdEvPrice().Average)

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
                            universalis.GetMarketInfo(world, mr.Item)
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

        using (cmdArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("#ssc",
                                    "计算部分道具兑换的价格",
                                    "兑换所需道具的名称或ID，只处理1个
可以设置查询服务器，已有服务器见#ff14help")>]
    member x.HandleSSS(cmdArg : CommandEventArgs) =
        let sc = SpecialShopCollection.Instance

        let cfg = CommandUtils.XivConfig(cmdArg)
        let world = cfg.GetWorld()

        if cfg.CommandLine.Length = 0 then
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
            let ret = strToItem (cfg.CommandLine.[0])

            match ret with
            | None -> cmdArg.AbortExecution(ModuleError, "找不到物品{0}", cfg.CommandLine.[0])
            | Some reqi ->
                let ia = sc.SearchByCostItemId(reqi.Id)

                if ia.Length = 0 then cmdArg.AbortExecution(InputError, "{0} 不能兑换道具", reqi.Name)

                let tt =
                    TextTable(
                        "兑换物品",
                        RightAlignCell "价格",
                        RightAlignCell "最低",
                        RightAlignCell "兑换价值",
                        RightAlignCell "更新时间"
                    )

                tt.AddPreTable(
                    sprintf "兑换道具:%s 土豆：%s/%s" reqi.Name world.DataCenter world.WorldName
                )

                ia
                |> Array.map
                    (fun info ->
                        let recv = itemCol.GetByItemId(info.ReceiveItem)

                        let market =
                            universalis.GetMarketInfo(world, recv)
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

    [<CommandHandlerMethodAttribute("#理符",
                                    "计算制作理符利润（只查询70级以上的基础材料）",
                                    "#理符 [职业名] [服务器名]",
                                    Disabled = true)>]
    member x.HandleCraftLeve(cmdArg : CommandEventArgs) =
        let cfg = CommandUtils.XivConfig(cmdArg)
        //let world = cfg.GetWorld()

        let leves =
            cfg.CommandLine
            |> Array.tryHead
            |> Option.map
                (fun x -> ClassJobMapping.ClassJobMappingCollection.Instance.TrySearchByName(x))
            |> Option.flatten
            |> Option.map (fun job -> CraftLeve.CraftLeveInfoCollection.Instance.GetByClassJob(job))

        if leves.IsNone then cmdArg.AbortExecution(InputError, "未设置职业或职业无效")

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
