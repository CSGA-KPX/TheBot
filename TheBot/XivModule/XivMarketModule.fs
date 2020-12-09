﻿namespace TheBot.Module.XivMarketModule

open System

open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

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

    let marketInfo = CompundMarketInfo.MarketInfoCollection.Instance

    let isNumber (str : string) =
        if str.Length <> 0 then String.forall (Char.IsDigit) str
        else false

    let strToItem (str : string) =
        if isNumber (str) then itemCol.TryGetByItemId(Convert.ToInt32(str))
        else itemCol.TryGetByName(str.TrimEnd(CommandUtils.XivSpecialChars))

    /// 给物品名备注上NPC价格
    let tryLookupNpcPrice (item : Item.ItemRecord) =
        let ret = gilShop.TryLookupByItem(item)
        if ret.IsSome then sprintf "%s(%i)" item.Name ret.Value.Ask
        else item.Name

    [<CommandHandlerMethodAttribute("xivsrv", "设置默认查询的服务器", "")>]
    member x.HandleXivDefSrv(msgArg : CommandArgs) =
        let cfg = CommandUtils.XivConfig(msgArg)
        if cfg.IsWorldDefined then
            let cm = ConfigManager(ConfigOwner.User (msgArg.MessageEvent.UserId))
            let world = cfg.GetWorld()
            cm.Put(CommandUtils.defaultServerKey, world)
            msgArg.QuickMessageReply(sprintf "%s的默认服务器设置为%s" msgArg.MessageEvent.DisplayName world.WorldName)
        else
            msgArg.QuickMessageReply("没有指定服务器或服务器名称不正确")

    [<CommandHandlerMethodAttribute("fm", "FF14市场查询", "")>]
    member x.HandleXivMarket(msgArg : CommandArgs) =
        let cfg = CommandUtils.XivConfig(msgArg)
        let worlds = cfg.GetWorlds()

        let tt = TextTable( LeftAlignCell "物品",
                            LeftAlignCell "土豆",
                            RightAlignCell "总体出售",
                            RightAlignCell "HQ出售",
                            RightAlignCell "总体交易",
                            RightAlignCell "HQ交易",
                            RightAlignCell "更新时间" )
        for str in cfg.CommandLine do 
            let item = strToItem str
            if item.IsNone then msgArg.AbortExecution(ModuleError, "找不到物品:{0}，请尝试#is {0}", str)
            let i = item.Value
            for w in worlds do 
                let tradelog =
                    let data = marketInfo.GetTradeLogs(w, i) |> Array.map (MarketUtils.MarketData.Trade)
                    MarketUtils.MarketAnalyzer(i, w, data)
                let listing =
                    let data = marketInfo.GetMarketListings(w, i) |> Array.map (MarketUtils.MarketData.Order)
                    MarketUtils.MarketAnalyzer(i, w, data)

                let name = i.Name
                let srvName = w.WorldName
                let mutable update = TimeSpan.MaxValue

                let mutable totalListing = box <| RightAlignCell "--"
                let mutable hqListing = box  <| RightAlignCell "--"

                if not listing.IsEmpty then
                    totalListing <- box <| listing.TakeVolume(25).StdEvPrice().Round().Average
                    hqListing <- box <| listing.TakeHQ().TakeVolume(25).StdEvPrice().Round().Average
                    update <- min update (listing.LastUpdateTime())

                let mutable totalTrade = box  <| RightAlignCell "--"
                let mutable hqTotalTrade = box  <| RightAlignCell "--"

                if not tradelog.IsEmpty then
                    totalTrade <- box <| tradelog.StdEvPrice().Round().Average
                    hqTotalTrade <- box <| tradelog.TakeHQ().StdEvPrice().Round().Average
                    update <- min update (tradelog.LastUpdateTime())

                let updateVal = if update = TimeSpan.MaxValue then box  <| RightAlignCell "--" else box update

                tt.AddRow(name, srvName, totalListing, hqListing, totalTrade, hqTotalTrade, updateVal)
        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun ret -> ret.Write(tt))

    [<CommandHandlerMethodAttribute("r", "根据表达式汇总多个物品的材料，不查询价格", "")>]
    [<CommandHandlerMethodAttribute("rr", "根据表达式汇总多个物品的基础材料，不查询价格", "")>]
    [<CommandHandlerMethodAttribute("rc", "计算物品基础材料成本", "物品Id或全名...")>]
    [<CommandHandlerMethodAttribute("rrc", "计算物品基础材料成本", "物品Id或全名...")>]
    member _.GeneralRecipeCalculator(msgArg : CommandArgs) = 
        let doCalculateCost = msgArg.CommandName = "rrc" || msgArg.CommandName = "rc"
        let materialFunc = 
            if msgArg.CommandName = "rr" || msgArg.CommandName = "rrc" then
                fun (item : Item.ItemRecord) -> rm.TryGetRecipeRec(item, 1.0)
            else
                fun (item : Item.ItemRecord) -> rm.TryGetRecipe(item)

        let cfg = CommandUtils.XivConfig(msgArg)
        let world = cfg.GetWorld()

        let tt = 
            if doCalculateCost then
                TextTable("物品", "价格", "数量", "小计", "更新时间")
            else
                TextTable("物品", "数量", "更新时间")

        if doCalculateCost then
            tt.AddPreTable(sprintf "服务器：%s" world.WorldName)

        let product = XivExpression.ItemAccumulator()
        let acc = XivExpression.ItemAccumulator()
        for str in cfg.CommandLine do
            match xivExpr.TryEval(str) with
            | Error err -> raise err
            | Ok(Number i) -> tt.AddPreTable(sprintf "计算结果为数字%f，物品Id请加#" i)
            | Ok(Accumulator a) ->
                for kv in a do
                    product.AddOrUpdate(kv.Key, kv.Value)
                    let (item, runs) = kv.Key, kv.Value
                    let recipe = materialFunc(item)
                    if recipe.IsNone then
                        tt.AddPreTable(sprintf "%s 没有生产配方" item.Name)
                    else
                        for m in recipe.Value.Input do
                            acc.AddOrUpdate(m.Item, m.Quantity * runs)

        let mutable sum = MarketUtils.StdEv.Zero
        for kv in acc |> Seq.sortBy (fun kv -> kv.Key.Id) do 
            let item = kv.Key
            let quantity = kv.Value
            let market = 
                if doCalculateCost then 
                    let data = marketInfo.GetMarketListings(world, item) |> Array.map (MarketUtils.MarketData.Order)
                    let m = MarketUtils.MarketAnalyzer(item, world, data)
                    Some (if m.IsEmpty then m else m.TakeVolume())
                else
                    None
            RowBuilder()
                .Add(item |> tryLookupNpcPrice)
                .AddCond(doCalculateCost, if market.Value.IsEmpty then
                                            box <| RightAlignCell "--"
                                          else
                                            box <| market.Value.StdEvPrice().Round().Average)
                .Add(quantity)
                .AddCond(doCalculateCost, if market.Value.IsEmpty then
                                            box <| RightAlignCell "--"
                                          else
                                            let subtotal = market.Value.StdEvPrice().Round() * quantity
                                            sum <- sum + subtotal
                                            box subtotal.Average)
                .AddCond(doCalculateCost, if market.Value.IsEmpty then
                                            box <| RightAlignCell "--"
                                          else
                                            box <| market.Value.LastUpdateTime())
            |> tt.AddRow

        if doCalculateCost then
            tt.AddRowFill("成本总计", sum.Average )
            let totalSell = product |> Seq.sumBy (fun kv -> 
                let data = marketInfo.GetMarketListings(world, kv.Key) |> Array.map (MarketUtils.MarketData.Order)
                let m = MarketUtils.MarketAnalyzer(kv.Key, world, data)
                (if m.IsEmpty then m else m.TakeVolume()).StdEvPrice().Round() * kv.Value)
            tt.AddRowFill("卖出价格", totalSell.Average )
            tt.AddRowFill("税前利润", (totalSell - sum).Average )

        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))

    [<CommandHandlerMethodAttribute("ssc", "计算部分道具兑换的价格", "兑换所需道具的名称或ID，只处理1个")>]
    member x.HandleSSS(msgArg : CommandArgs) =
        let sc = SpecialShop.SpecialShopCollection.Instance
        let cfg = CommandUtils.XivConfig(msgArg)
        let world = cfg.GetWorld()

        if cfg.CommandLine.Length = 0 then
            //回复所有可交易道具
            let tt = TextTable("名称", "id")
            tt.AddPreTable("可交换道具：")
            for item in sc.AllCostItems() do 
                tt.AddRow(item.Name, item.Id.ToString())
            using (msgArg.OpenResponse()) (fun x -> x.Write(tt))
        else
            let ret = strToItem(cfg.CommandLine.[0])
            match ret with
            | None -> msgArg.AbortExecution(ModuleError, "找不到物品{0}", cfg.CommandLine.[0])
            | Some reqi ->
                let ia = sc.SearchByCostItemId(reqi.Id)
                if ia.Length = 0 then
                    msgArg.AbortExecution(InputError, "{0} 不能兑换道具", reqi.Name)
                let tt= TextTable("兑换物品", 
                                    RightAlignCell "价格", 
                                    RightAlignCell "最低", 
                                    RightAlignCell "兑换价值", 
                                    RightAlignCell "更新时间")
                tt.AddPreTable(sprintf "兑换道具:%s 土豆：%s/%s" reqi.Name world.DataCenter world.WorldName )
                for info in ia do
                    let recv = itemCol.GetByItemId(info.ReceiveItem)
                    let market = 
                        let data = marketInfo.GetMarketListings(world, recv) |> Array.map (MarketUtils.MarketData.Order)
                        let m = MarketUtils.MarketAnalyzer(recv, world, data)
                        if m.IsEmpty then m else m.TakeVolume()
                    let isEmpty = market.IsEmpty
                    let notEmpty = not isEmpty

                    let def = RightAlignCell "--"

                    RowBuilder()
                        .Add(recv.Name)
                        .AddIf(notEmpty, market.StdEvPrice().Round().Average
                                       , def)
                        .AddIf(notEmpty, market.MinPrice()
                                       , def)
                        .AddIf(notEmpty, (market.StdEvPrice().Round() * (float <| info.ReceiveCount) / (float <| info.CostCount)).Average
                                       , def)
                        .AddIf(notEmpty, market.LastUpdateTime()
                                       , def)
                    |> tt.AddRow
                using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(tt))
    
    [<CommandHandlerMethodAttribute("伊修加德重建采集", "计算采集利润", "无参数")>]
    member x.HandleRebuildGathering(msgArg : CommandArgs) = 
        let cfg = CommandUtils.XivConfig(msgArg)
        let world = cfg.GetWorld()

        let items = 
            itemCol.SearchByName("第二期重建用的")
            |> Array.filter (fun item -> item.Name.Contains("（检）"))

        if items.Length = 0 then msgArg.AbortExecution(ModuleError, "一个物品都没找到，这不科学，请联系开发者")

        use ret = msgArg.OpenResponse(ForceImage)
        ret.WriteLine("价格有延迟，算法不稳定，市场有风险, 投资需谨慎")
        ret.WriteLine(sprintf "当前服务器：%s" world.WorldName)

        let tt = TextTable("名称", RightAlignCell "平均", RightAlignCell "低", RightAlignCell "更新时间")

        for item in items do 
            let data = marketInfo.GetMarketListings(world, item) |> Array.map (MarketUtils.MarketData.Order)
            let orders = MarketUtils.MarketAnalyzer(item, world, data)
            if orders.Data.Length = 0 then
                tt.AddRow(item.Name, "--", "--", "无记录")
            else
                let vol = orders.TakeVolume(25)
                let avg = vol.StdEvPrice().Round().Average |> int
                let low = vol.MinPrice()
                let upd = vol.LastUpdateTime()
                tt.AddRow(item.Name, avg, low, upd)

        ret.Write(tt)