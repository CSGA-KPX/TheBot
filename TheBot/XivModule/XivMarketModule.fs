namespace TheBot.Module.XivMarketModule

open System

open KPX.FsCqHttp.Handler.CommandHandlerBase

open BotData.XivData

open TheBot.Utils.TextTable
open TheBot.Utils.Config
open TheBot.Utils.RecipeRPN

open TheBot.Module.XivModule.Utils

type XivMarketModule() =
    inherit CommandHandlerBase()

    let rm = Recipe.RecipeManager.GetInstance()
    let itemCol = Item.ItemCollection.Instance
    let gilShop = GilShop.GilShopCollection.Instance
    let xivExpr = XivExpression.XivExpression()

    let isNumber (str : string) =
        if str.Length <> 0 then String.forall (Char.IsDigit) str
        else false

    let strToItemResult (str : string) =
        let ret =
            if isNumber (str) then itemCol.TryGetByItemId(Convert.ToInt32(str))
            else itemCol.TryGetByName(str.TrimEnd(CommandUtils.XivSpecialChars))
        if ret.IsSome then Ok ret.Value
        else Error str

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
            msgArg.QuickMessageReply(sprintf "%s的默认服务器设置为%s" msgArg.MessageEvent.GetNicknameOrCard world.WorldName)
        else
            msgArg.QuickMessageReply("没有指定服务器或服务器名称不正确")

    member _.GeneralMarketPrinter(msgArg : CommandArgs,
                                  headers : (CellType * (MarketUtils.MarketAnalyzer -> obj)) [],
                                  fetchFunc : Item.ItemRecord * World.World ->Result<MarketUtils.MarketAnalyzer, exn>) = 

        let att = AutoTextTable<MarketUtils.MarketAnalyzer>(headers)

        let cfg = CommandUtils.XivConfig(msgArg)
        let world = cfg.GetWorld()

        att.AddPreTable(sprintf "服务器：%s" world.WorldName)

        let rets =
            cfg.CommandLine
            |> Array.map (strToItemResult >> Result.map (fun i -> fetchFunc(i, world)))

        for ret in rets do 
            match ret with
            | Error str -> att.AddPreTable(sprintf "找不到物品%s，请尝试#is %s" str str)
            | Ok ma ->
                match ma with
                | Error exn -> raise exn
                | Ok ma ->
                    if ma.IsEmpty then att.AddRowPadding(ma.ItemRecord.Name, "无记录")
                    else att.AddObject(ma)

        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(att))

    [<CommandHandlerMethodAttribute("tradelog", "查询交易记录", "物品Id或全名...")>]
    member x.HandleTradelog(msgArg : CommandArgs) =
        let hdrs = 
            [|
                LeftAlignCell "名称", fun (ma : MarketUtils.MarketAnalyzer) -> box(ma.ItemRecord |> tryLookupNpcPrice)
                RightAlignCell "平均", fun ma -> box(ma.StdEvPrice())
                RightAlignCell "低", fun ma -> box(ma.MinPrice())
                RightAlignCell "高", fun ma -> box(ma.MaxPrice())
                LeftAlignCell "更新时间", fun ma -> box(ma.LastUpdateTime())
            |]

        let func = fun (i,w) -> MarketUtils.MarketAnalyzer.FetchTradesWorld(i, w)
        x.GeneralMarketPrinter(msgArg, hdrs, func)

    [<CommandHandlerMethodAttribute("market", "查询市场订单", "物品Id或全名...")>]
    member x.HandleMarket(msgArg : CommandArgs) =
        let hdrs = 
            [|
                LeftAlignCell "名称", fun (ma : MarketUtils.MarketAnalyzer) -> box(ma.ItemRecord |> tryLookupNpcPrice)
                RightAlignCell "总体", fun ma -> box(ma.TakeVolume(25).StdEvPrice())
                RightAlignCell "HQ", fun ma -> box(ma.TakeHQ().TakeVolume(25).StdEvPrice())
                LeftAlignCell "更新时间", fun ma -> box(ma.LastUpdateTime())
            |]

        let func = fun (i,w) -> MarketUtils.MarketAnalyzer.FetchOrdersWorld(i, w)
        x.GeneralMarketPrinter(msgArg, hdrs, func)

    member _.GeneralCrossWorldMarketPrinter(msgArg : CommandArgs,
                                  headers : (CellType * (MarketUtils.MarketAnalyzer -> obj)) [],
                                  fetchFunc : Item.ItemRecord ->Result<MarketUtils.MarketAnalyzer[], exn>) = 
        let att = AutoTextTable<MarketUtils.MarketAnalyzer>(headers)
        let cfg = CommandUtils.XivConfig(msgArg)
        let rets =
            cfg.CommandLine
            |> Array.map (strToItemResult >> Result.map (fun i -> fetchFunc(i)))

        for ret in rets do 
            match ret with
            | Error str -> att.AddPreTable(sprintf "找不到物品%s，请尝试#is %s" str str)
            | Ok wma ->
                match wma with
                | Error exn -> raise exn
                | Ok wma ->
                    let sorted = 
                        wma
                        |> Array.groupBy (fun (m) -> World.WorldToDC.[m.World])
                        |> Array.sortBy fst
                    for dc, ma in sorted do 
                        att.AddRowPadding(dc)
                        for m in ma do 
                            if m.IsEmpty then att.AddRowPadding(m.ItemRecord.Name, m.World.WorldName, "无记录")
                            else att.AddObject(m)

        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(att))

    [<CommandHandlerMethodAttribute("alltradelog", "查询全服交易记录", "物品Id或全名...")>]
    member x.HandleTradelogCrossWorld(msgArg : CommandArgs) =
        let hdrs = 
            [|
                LeftAlignCell "名称", fun (ma : MarketUtils.MarketAnalyzer) -> box(ma.ItemRecord  |> tryLookupNpcPrice)
                LeftAlignCell "土豆", fun (ma : MarketUtils.MarketAnalyzer) -> box(ma.World.WorldName)
                RightAlignCell "平均", fun ma -> box(ma.StdEvPrice())
                RightAlignCell "低", fun ma -> box(ma.MinPrice())
                RightAlignCell "高", fun ma -> box(ma.MaxPrice())
                LeftAlignCell "更新时间", fun ma -> box(ma.LastUpdateTime())
            |]

        let func = fun (i) -> MarketUtils.MarketAnalyzer.FetchTradesAllWorld(i)
        x.GeneralCrossWorldMarketPrinter(msgArg, hdrs, func)

    [<CommandHandlerMethodAttribute("allmarket", "查询全服市场订单", "物品Id或全名...")>]
    member x.HandleMarketCrossWorld(msgArg : CommandArgs) =
        let hdrs = 
            [|
                LeftAlignCell "名称", fun (ma : MarketUtils.MarketAnalyzer) -> box(ma.ItemRecord |> tryLookupNpcPrice)
                LeftAlignCell "土豆", fun ma -> box(ma.World.WorldName)
                RightAlignCell "总体", fun ma -> box(ma.TakeVolume().StdEvPrice())
                RightAlignCell "HQ", fun ma -> box(ma.TakeHQ().TakeVolume().StdEvPrice())
                LeftAlignCell "更新时间", fun ma -> box(ma.LastUpdateTime())
            |]

        let func = fun (i) -> MarketUtils.MarketAnalyzer.FetchOrdersAllWorld(i)
        x.GeneralCrossWorldMarketPrinter(msgArg, hdrs, func)


    [<CommandHandlerMethodAttribute("r", "根据表达式汇总多个物品的材料，不查询价格", "")>]
    [<CommandHandlerMethodAttribute("rr", "根据表达式汇总多个物品的基础材料，不查询价格", "")>]
    [<CommandHandlerMethodAttribute("rc", "计算物品基础材料成本", "物品Id或全名...")>]
    [<CommandHandlerMethodAttribute("rrc", "计算物品基础材料成本", "物品Id或全名...")>]
    member _.GeneralRecipeCalculator(msgArg : CommandArgs) = 
        let doCalculateCost = msgArg.IsCommand("rrc") || msgArg.IsCommand("rc")
        let materialFunc = 
            if msgArg.IsCommand("rr") || msgArg.IsCommand("rrc") then
                fun (item : Item.ItemRecord) -> rm.GetMaterialsRec(item)
            else
                fun (item : Item.ItemRecord) -> rm.GetMaterialsOne(item)

        let cfg = CommandUtils.XivConfig(msgArg)
        let world = cfg.GetWorld()

        let mutable cur = None
        let updateCur(item : Item.ItemRecord) = 
            let ret = MarketUtils.MarketAnalyzer.FetchOrdersWorld(item, world)
            match ret with
            | Error exn -> raise exn
            | Ok m  -> cur <- Some (if m.IsEmpty then m else m.TakeVolume())
            
        let mutable sum = MarketUtils.StdEv.Zero
        let hdrs = 
            [|
                yield LeftAlignCell "物品", fun (item : Item.ItemRecord, amount : float) -> box (item |> tryLookupNpcPrice)

                if doCalculateCost then
                    yield LeftAlignCell "价格", fun (item, amount) ->
                            updateCur(item)
                            if cur.Value.IsEmpty then box "无记录"
                            else box (cur.Value.StdEvPrice())

                yield LeftAlignCell "数量", snd >> box

                if doCalculateCost then
                    yield LeftAlignCell "小计", fun (item, amount) ->
                            if cur.Value.IsEmpty then box <| RightAlignCell "--"
                            else
                                let subtotal = cur.Value.StdEvPrice() * amount
                                sum <- sum + subtotal
                                box subtotal

                    yield LeftAlignCell "更新时间", fun (item, amount) ->
                            if cur.Value.IsEmpty then box <| RightAlignCell "--"
                            else box (cur.Value.LastUpdateTime())
            |]

        let att = AutoTextTable<Item.ItemRecord * float>(hdrs)

        if doCalculateCost then
            att.AddPreTable(sprintf "服务器：%s" world.WorldName)

        let acc = XivExpression.ItemAccumulator()
        for str in cfg.CommandLine do
            match xivExpr.TryEval(str) with
            | Error err -> raise err
            | Ok(Number i) -> att.AddPreTable(sprintf "计算结果为数字%f，物品Id请加#" i)
            | Ok(Accumulator a) ->
                for kv in a do
                    let (item, runs) = kv.Key, kv.Value
                    let recipe = materialFunc(item)
                    if recipe.Length = 0 then
                        att.AddPreTable(sprintf "%s 没有生产配方" item.Name)
                    else
                        for (i, r) in recipe do
                            acc.AddOrUpdate(i, r * runs)

        for kv in acc |> Seq.sortBy (fun kv -> kv.Key.Id) do 
            att.AddObject(kv.Key, kv.Value)

        if doCalculateCost then att.AddRowPadding("总计", sum)

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
                let ret = MarketUtils.MarketAnalyzer.FetchOrdersWorld(item, world)
                match ret with
                | Error exn -> raise exn
                | Ok m  -> cur <- Some (if m.IsEmpty then m else m.TakeVolume())

            let hdrs = 
                [|
                    "兑换", fun (info : SpecialShop.SpecialShopInfo) ->
                                curItem <- Some (itemCol.GetByItemId(info.ReceiveItem))
                                updateCur(curItem.Value)
                                box (curItem.Value.Name)
                    "价格", fun _ -> 
                                if cur.Value.IsEmpty then
                                    box  "--"
                                else
                                    box (cur.Value.TakeVolume().StdEvPrice())
                    "最低", fun _ ->
                                if cur.Value.IsEmpty then
                                    box "--"
                                else
                                box (cur.Value.MinPrice())
                    "价值", fun info ->
                                if cur.Value.IsEmpty then
                                    box "--"
                                else
                                    box (cur.Value.TakeVolume().StdEvPrice() * (float <|info.ReceiveCount) / (float <|info.CostCount))
                    "更新", fun _ ->
                                if cur.Value.IsEmpty then
                                    box "--"
                                else
                                    box (cur.Value.LastUpdateTime())
                |]

            let att = AutoTextTable<SpecialShop.SpecialShopInfo>(hdrs)

            let ret = strToItemResult(cfg.CommandLine.[0])
            match ret with
            | Error x -> failwithf "找不到物品 %s" x
            | Ok item ->
                let ia = sc.SearchByCostItemId(item.Id)
                if ia.Length = 0 then
                    failwithf "%s 不能兑换道具" item.Name
                for info in ia do 
                    att.AddObject(info)
            using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun x -> x.Write(att))