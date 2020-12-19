namespace TheBot.Module.XivMarketModule

open System

open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

open BotData.CommonModule.Recipe
open BotData.XivData

open TheBot.Utils.Config
open TheBot.Utils.RecipeRPN

open TheBot.Module.XivModule.Utils

type XivMarketModule() =
    inherit CommandHandlerBase()

    let rm = Recipe.XivRecipeManager.Instance
    let itemCol = Item.ItemCollection.Instance
    let gilShop = GilShop.GilShopCollection.Instance
    let xivExpr = XivExpression.XivExpression()

    let padNumber = box <| RightAlignCell "--"
    let padOnNan f =
        if Double.IsNaN(f) then padNumber else box f

    let isNumber (str : string) =
        if str.Length <> 0 then String.forall (Char.IsDigit) str else false

    let strToItem (str : string) =
        if isNumber (str)
        then itemCol.TryGetByItemId(Convert.ToInt32(str))
        else itemCol.TryGetByName(str.TrimEnd(CommandUtils.XivSpecialChars))

    /// 给物品名备注上NPC价格
    let tryLookupNpcPrice (item : Item.ItemRecord) =
        let ret = gilShop.TryLookupByItem(item)
        if ret.IsSome then sprintf "%s(%i)" item.Name ret.Value.Ask else item.Name

    [<CommandHandlerMethodAttribute("xivsrv", "设置默认查询的服务器", "")>]
    member x.HandleXivDefSrv(cmdArg : CommandEventArgs) =
        let cfg = CommandUtils.XivConfig(cmdArg)

        if cfg.IsWorldDefined then
            let cm =
                ConfigManager(ConfigOwner.User(cmdArg.MessageEvent.UserId))

            let world = cfg.GetWorld()
            cm.Put(CommandUtils.defaultServerKey, world)
            cmdArg.QuickMessageReply(sprintf "%s的默认服务器设置为%s" cmdArg.MessageEvent.DisplayName world.WorldName)
        else
            cmdArg.QuickMessageReply("没有指定服务器或服务器名称不正确")

    [<CommandHandlerMethodAttribute("fm", "FF14市场查询", "")>]
    member x.HandleXivMarket(cmdArg : CommandEventArgs) =
        let cfg = CommandUtils.XivConfig(cmdArg)
        let worlds = cfg.GetWorlds()

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

        for str in cfg.CommandLine do
            match xivExpr.TryEval(str) with
            | Error err -> raise err
            | Ok (Number i) -> tt.AddPreTable(sprintf "计算结果为数字%f，物品Id请加#" i)
            | Ok (Accumulator a) ->
                for i in a do
                    acc.Update(i)

        if acc.Count * worlds.Length >= 20 then cmdArg.AbortExecution(InputError, "查询数量超过上线")

        for world in worlds do
            let mutable sumListingAll, sumListingHq = 0.0, 0.0
            let mutable sumTradeAll, sumTradeHq = 0.0, 0.0

            for mr in acc do
                let tradelog =
                    MarketUtils.MarketAnalyzer.GetTradeLog(world, mr.Item)

                let listing =
                    MarketUtils.MarketAnalyzer.GetMarketListing(world, mr.Item)

                let mutable updated = TimeSpan.MaxValue

                let lstAll =
                    listing.TakeVolume(25).StdEvPrice().Round()
                        .Average

                let lstHq =
                    listing
                        .TakeHQ()
                        .TakeVolume(25)
                        .StdEvPrice()
                        .Round()
                        .Average

                updated <- min updated (listing.LastUpdateTime())
                sumListingAll <- sumListingAll + lstAll
                sumListingHq <- sumListingHq + lstHq

                let logAll = tradelog.StdEvPrice().Round().Average

                let logHq =
                    tradelog.TakeHQ().StdEvPrice().Round().Average

                updated <- min updated (tradelog.LastUpdateTime())
                sumTradeAll <- sumTradeAll + logAll
                sumTradeHq <- sumTradeHq + logHq

                let updateVal =
                    if updated = TimeSpan.MaxValue then padNumber else box updated

                tt.AddRow(
                    RowBuilder()
                        .Add(mr.Item.Name)
                        .Add(world.WorldName)
                        .Add(mr.Quantity)
                        .Add(padOnNan lstAll)
                        .Add(padOnNan lstHq)
                        .Add(padOnNan logAll)
                        .Add(padOnNan logHq)
                        .Add(updateVal)
                )

            if acc.Count >= 2 then
                tt.AddRow(
                    RowBuilder()
                        .Add("合计")
                        .Add("--")
                        .Add(padNumber)
                        .Add(padOnNan sumListingAll)
                        .Add(padOnNan sumListingHq)
                        .Add(padOnNan sumTradeAll)
                        .Add(padOnNan sumTradeHq)
                        .Add(padNumber)
                )

        using (cmdArg.OpenResponse(cfg.IsImageOutput)) (fun ret -> ret.Write(tt))

    [<CommandHandlerMethodAttribute("r", "根据表达式汇总多个物品的材料，不查询价格", "")>]
    [<CommandHandlerMethodAttribute("rr", "根据表达式汇总多个物品的基础材料，不查询价格", "")>]
    [<CommandHandlerMethodAttribute("rc", "计算物品基础材料成本", "物品Id或全名...")>]
    [<CommandHandlerMethodAttribute("rrc", "计算物品基础材料成本", "物品Id或全名...")>]
    member _.GeneralRecipeCalculator(cmdArg : CommandEventArgs) =
        let doCalculateCost =
            cmdArg.CommandName = "rrc"
            || cmdArg.CommandName = "rc"

        let materialFunc =
            if cmdArg.CommandName = "rr"
               || cmdArg.CommandName = "rrc" then
                fun (item : Item.ItemRecord) -> rm.TryGetRecipeRec(item, ByItem 1.0)
            else
                fun (item : Item.ItemRecord) -> rm.TryGetRecipe(item)

        let cfg = CommandUtils.XivConfig(cmdArg)
        let world = cfg.GetWorld()

        let tt =
            if doCalculateCost then TextTable("物品", "价格", "数量", "小计", "更新时间") else TextTable("物品", "数量")

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
            let item = mr.Item
            let quantity = mr.Quantity

            let market =
                if doCalculateCost then
                    MarketUtils
                        .MarketAnalyzer
                        .GetMarketListing(world, item)
                        .TakeVolume()
                    |> Some
                else
                    None

            RowBuilder()
                .Add(item |> tryLookupNpcPrice)
                .AddCond(doCalculateCost,
                         lazy
                             (if market.Value.IsEmpty then
                                 box <| RightAlignCell "--"
                              else
                                  box <| market.Value.StdEvPrice().Round().Average))
                .Add(quantity)
                .AddCond(doCalculateCost,
                         lazy
                             (if market.Value.IsEmpty then
                                 box <| RightAlignCell "--"
                              else
                                  let subtotal =
                                      market.Value.StdEvPrice().Round() * quantity

                                  sum <- sum + subtotal
                                  box subtotal.Average))
                .AddCond(doCalculateCost,
                         lazy
                             (if market.Value.IsEmpty then
                                 box <| RightAlignCell "--"
                              else
                                  box <| market.Value.LastUpdateTime()))
            |> tt.AddRow

        if doCalculateCost then
            tt.AddRowFill("成本总计", sum.Average)

            let totalSell =
                product
                |> Seq.sumBy
                    (fun mr ->
                        let lst =
                            MarketUtils
                                .MarketAnalyzer
                                .GetMarketListing(world, mr.Item)
                                .TakeVolume()

                        lst.StdEvPrice().Round() * mr.Quantity)

            tt.AddRowFill("卖出价格", totalSell.Average)
            tt.AddRowFill("税前利润", (totalSell - sum).Average)

        using (cmdArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("ssc", "计算部分道具兑换的价格", "兑换所需道具的名称或ID，只处理1个")>]
    member x.HandleSSS(cmdArg : CommandEventArgs) =
        let sc =
            SpecialShop.SpecialShopCollection.Instance

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
                sc.AllCostItems() |> Seq.chunkBySize headerCol

            for chunk in chunks do
                let rb = RowBuilder()

                for item in chunk do
                    rb.Add(item.Id).Add(item.Name) |> ignore
                for i = 0 to headerCol - chunk.Length - 1 do
                    rb.AddRightAlign("--").AddLeftAlign("--")
                    |> ignore

                tt.AddRow(rb)

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

                tt.AddPreTable(sprintf "兑换道具:%s 土豆：%s/%s" reqi.Name world.DataCenter world.WorldName)

                for info in ia do
                    let recv = itemCol.GetByItemId(info.ReceiveItem)

                    let market =
                        MarketUtils
                            .MarketAnalyzer
                            .GetMarketListing(world, recv)
                            .TakeVolume()

                    let isEmpty = market.IsEmpty
                    let notEmpty = not isEmpty

                    let def = lazy (RightAlignCell "--")

                    RowBuilder()
                        .Add(recv.Name)
                        .Add(padOnNan(market.StdEvPrice().Round().Average))
                        .Add(padOnNan(market.MinPrice()))
                        .Add(padOnNan
                                 ((market.StdEvPrice().Round()
                                   * (float <| info.ReceiveCount)
                                   / (float <| info.CostCount))
                                     .Average))
                        .AddIf(notEmpty, lazy (market.LastUpdateTime()), def)
                    |> tt.AddRow

                using (cmdArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))
