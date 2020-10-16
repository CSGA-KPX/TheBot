namespace TheBot.Module.XivMarketModule

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

    let rm = Recipe.RecipeManager.GetInstance()
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

    member _.GeneralMarketPrinter(msgArg : CommandArgs,
                                  headers : (CellType * (MarketUtils.MarketAnalyzer -> obj)) [],
                                  fetchFunc : Item.ItemRecord * World.World -> MarketUtils.MarketAnalyzer) = 

        let att = AutoTextTable<MarketUtils.MarketAnalyzer>(headers)

        let cfg = CommandUtils.XivConfig(msgArg)
        let worlds = cfg.GetWorlds()

        let rets =
            cfg.CommandLine
            |> Array.collect (fun str ->
                let item = strToItem str
                if item.IsNone then msgArg.AbortExecution(ModuleError, "找不到物品:{0}，请尝试#is {0}", str)
                [|  for world in worlds do
                        fetchFunc(item.Value, world) |] )

        for ma in rets do 
            if ma.IsEmpty then att.AddRowPadding(ma.ItemRecord.Name, ma.World.WorldName,  "无记录")
            else att.AddObject(ma)

        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(att))

    [<CommandHandlerMethodAttribute("tradelog", "查询交易记录", "物品Id或全名...")>]
    member x.HandleTradelog(msgArg : CommandArgs) =
        let hdrs = 
            [|
                LeftAlignCell "名称", fun (ma : MarketUtils.MarketAnalyzer) -> box(ma.ItemRecord |> tryLookupNpcPrice)
                LeftAlignCell "土豆", fun ma -> box(ma.World.WorldName)
                RightAlignCell "平均", fun ma -> box(ma.StdEvPrice().Round().Average)
                RightAlignCell "低", fun ma -> box(ma.MinPrice())
                RightAlignCell "高", fun ma -> box(ma.MaxPrice())
                LeftAlignCell "更新时间", fun ma -> box(ma.LastUpdateTime())
            |]

        let func = fun (i,w) -> 
            let data = marketInfo.GetTradeLogs(w, i) |> Array.map (MarketUtils.MarketData.Trade)
            MarketUtils.MarketAnalyzer(i, w, data)
        x.GeneralMarketPrinter(msgArg, hdrs, func)

    [<CommandHandlerMethodAttribute("market", "查询市场订单", "物品Id或全名...")>]
    member x.HandleMarket(msgArg : CommandArgs) =
        let hdrs = 
            [|
                LeftAlignCell "名称", fun (ma : MarketUtils.MarketAnalyzer) -> box(ma.ItemRecord |> tryLookupNpcPrice)
                LeftAlignCell "土豆", fun ma -> box(ma.World.WorldName)
                RightAlignCell "总体", fun ma -> box(ma.TakeVolume(25).StdEvPrice().Round().Average)
                RightAlignCell "HQ", fun ma -> box(ma.TakeHQ().TakeVolume(25).StdEvPrice().Round().Average)
                LeftAlignCell "更新时间", fun ma -> box(ma.LastUpdateTime())
            |]

        let func = fun (i,w) -> 
            let data = marketInfo.GetMarketListings(w, i) |> Array.map (MarketUtils.MarketData.Order)
            MarketUtils.MarketAnalyzer(i, w, data)
        x.GeneralMarketPrinter(msgArg, hdrs, func)

    [<CommandHandlerMethodAttribute("r", "根据表达式汇总多个物品的材料，不查询价格", "")>]
    [<CommandHandlerMethodAttribute("rr", "根据表达式汇总多个物品的基础材料，不查询价格", "")>]
    [<CommandHandlerMethodAttribute("rc", "计算物品基础材料成本", "物品Id或全名...")>]
    [<CommandHandlerMethodAttribute("rrc", "计算物品基础材料成本", "物品Id或全名...")>]
    member _.GeneralRecipeCalculator(msgArg : CommandArgs) = 
        let doCalculateCost = msgArg.CommandName = "rrc" || msgArg.CommandName = "rc"
        let materialFunc = 
            if msgArg.CommandName = "rr" || msgArg.CommandName = "rrc" then
                fun (item : Item.ItemRecord) -> rm.GetMaterialsRec(item)
            else
                fun (item : Item.ItemRecord) -> rm.GetMaterialsOne(item)

        let cfg = CommandUtils.XivConfig(msgArg)
        let world = cfg.GetWorld()

        let mutable cur = None
        let updateCur(item : Item.ItemRecord) = 
            let data = marketInfo.GetMarketListings(world, item) |> Array.map (MarketUtils.MarketData.Order)
            let m = MarketUtils.MarketAnalyzer(item, world, data)
            cur <- Some (if m.IsEmpty then m else m.TakeVolume())
            
        let mutable sum = MarketUtils.StdEv.Zero
        let hdrs = 
            [|
                yield LeftAlignCell "物品", fun (item : Item.ItemRecord, _) -> box (item |> tryLookupNpcPrice)

                if doCalculateCost then
                    yield RightAlignCell "价格", fun (item, _) ->
                            updateCur(item)
                            if cur.Value.IsEmpty then box <| RightAlignCell "--"
                            else box (cur.Value.StdEvPrice().Round().Average)

                yield RightAlignCell "数量", snd >> box

                if doCalculateCost then
                    yield RightAlignCell "小计", fun (_, amount) ->
                            if cur.Value.IsEmpty then box <| RightAlignCell "--"
                            else
                                let subtotal = cur.Value.StdEvPrice().Round() * amount
                                sum <- sum + subtotal
                                box subtotal.Average

                    yield RightAlignCell "更新时间", fun (_, _) ->
                            if cur.Value.IsEmpty then box <| RightAlignCell "--"
                            else box <| RightAlignCell (cur.Value.LastUpdateTime())
            |]

        let att = AutoTextTable<Item.ItemRecord * float>(hdrs)

        if doCalculateCost then
            att.AddPreTable(sprintf "服务器：%s" world.WorldName)

        let product = XivExpression.ItemAccumulator()
        let acc = XivExpression.ItemAccumulator()
        for str in cfg.CommandLine do
            match xivExpr.TryEval(str) with
            | Error err -> raise err
            | Ok(Number i) -> att.AddPreTable(sprintf "计算结果为数字%f，物品Id请加#" i)
            | Ok(Accumulator a) ->
                for kv in a do
                    product.AddOrUpdate(kv.Key, kv.Value)
                    let (item, runs) = kv.Key, kv.Value
                    let recipe = materialFunc(item)
                    if recipe.Length = 0 then
                        att.AddPreTable(sprintf "%s 没有生产配方" item.Name)
                    else
                        for (i, r) in recipe do
                            acc.AddOrUpdate(i, r * runs)

        for kv in acc |> Seq.sortBy (fun kv -> kv.Key.Id) do 
            att.AddObject(kv.Key, kv.Value)

        if doCalculateCost then
            att.AddRowPadding("成本总计", sum.Average )
            let totalSell = product |> Seq.sumBy (fun kv -> 
                let data = marketInfo.GetMarketListings(world, kv.Key) |> Array.map (MarketUtils.MarketData.Order)
                let m = MarketUtils.MarketAnalyzer(kv.Key, world, data)
                (if m.IsEmpty then m else m.TakeVolume()).StdEvPrice().Round() * kv.Value)
            att.AddRowPadding("卖出价格", totalSell.Average )
            att.AddRowPadding("税前利润", (totalSell - sum).Average )

        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(att))

    [<CommandHandlerMethodAttribute("ssc", "计算部分道具兑换的价格", "兑换所需道具的名称或ID，只处理1个")>]
    member x.HandleSSS(msgArg : CommandArgs) =
        let sc = SpecialShop.SpecialShopCollection.Instance
        let cfg = CommandUtils.XivConfig(msgArg)
        let world = cfg.GetWorld()

        if cfg.CommandLine.Length = 0 then
            //回复所有可交易道具
            let tt = TextTable.FromHeader([|"名称"; "id"|])
            tt.AddPreTable("可交换道具：")
            for item in sc.AllCostItems() do 
                tt.AddRow(item.Name, item.Id.ToString())
            using (msgArg.OpenResponse()) (fun x -> x.Write(tt))
        else
            let mutable curItem = None
            let mutable cur = None
            let updateCur(item : Item.ItemRecord) = 
                let data = marketInfo.GetMarketListings(world, item) |> Array.map (MarketUtils.MarketData.Order)
                let m = MarketUtils.MarketAnalyzer(item, world, data)
                cur <- Some (if m.IsEmpty then m else m.TakeVolume())

            let hdrs = 
                [|
                    LeftAlignCell "兑换", fun (info : SpecialShop.SpecialShopInfo) ->
                                curItem <- Some (itemCol.GetByItemId(info.ReceiveItem))
                                updateCur(curItem.Value)
                                box (curItem.Value.Name)
                    RightAlignCell "价格", fun _ -> 
                                if cur.Value.IsEmpty then
                                    box  <| RightAlignCell "--"
                                else
                                    box (cur.Value.TakeVolume().StdEvPrice().Round().Average)
                    RightAlignCell "最低", fun _ ->
                                if cur.Value.IsEmpty then
                                    box  <| RightAlignCell "--"
                                else
                                box (cur.Value.MinPrice())
                    RightAlignCell "价值", fun info ->
                                if cur.Value.IsEmpty then
                                    box  <| RightAlignCell "--"
                                else
                                    box (cur.Value.TakeVolume().StdEvPrice().Round() * (float <|info.ReceiveCount) / (float <|info.CostCount)).Average
                    RightAlignCell "更新", fun _ ->
                                if cur.Value.IsEmpty then
                                    box  <| RightAlignCell "--"
                                else
                                    box  <| RightAlignCell (cur.Value.LastUpdateTime())
                |]

            let att = AutoTextTable<SpecialShop.SpecialShopInfo>(hdrs)

            let ret = strToItem(cfg.CommandLine.[0])
            match ret with
            | None -> msgArg.AbortExecution(ModuleError, "找不到物品{0}", cfg.CommandLine.[0])
            | Some item ->
                let ia = sc.SearchByCostItemId(item.Id)
                if ia.Length = 0 then
                    msgArg.AbortExecution(InputError, "{0} 不能兑换道具", item.Name)
                for info in ia do 
                    att.AddObject(info)
            using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(att))
    
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

        let tt = TextTable.FromHeader([|"名称"; "平均"; "低"; "更新时间"|])

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