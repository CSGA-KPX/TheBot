namespace TheBot.Module.XivModule

open System
open KPX.FsCqHttp.Handler.CommandHandlerBase
open XivData
open TheBot.Module.XivModule.Utils
open TheBot.Utils

type XivModule() =
    inherit CommandHandlerBase()
    let rm = Recipe.RecipeManager.GetInstance()
    let cutoff = 25
    let itemCol = Item.ItemCollection.Instance
    let gilShop = GilShop.GilShopCollection.Instance

    let isNumber (str : string) =
        if str.Length <> 0 then String.forall (Char.IsDigit) str
        else false

    let strToItemResult (str : string) =
        let ret =
            if isNumber (str) then itemCol.TryLookupById(Convert.ToInt32(str))
            else itemCol.TryLookupByName(str)
        if ret.IsSome then Ok ret.Value
        else Error str

    /// 给物品名备注上NPC价格
    let tryLookupNpcPrice (item : Item.ItemRecord) =
        let ret = gilShop.TryLookupByItem(item)
        if ret.IsSome then sprintf "%s(%i)" item.Name ret.Value.Ask
        else item.Name

    [<CommandHandlerMethodAttribute("tradelog", "查询交易记录", "物品Id或全名...")>]
    member x.HandleTradelog(msgArg : CommandArgs) =
        let sw = new IO.StringWriter()
        let (succ, world, args) = CommandUtils.GetWorldWithDefault(msgArg.Arguments)
        if succ then sw.WriteLine("服务器：{0}", world.WorldName)
        else sw.WriteLine("默认服务器：{0}", world.WorldName)
        let tt = TextTable.FromHeader([| "名称"; "平均"; "低"; "高"; "更新时间" |])

        let rets =
            args
            |> Array.map (strToItemResult >> Result.map (fun i -> MarketUtils.MarketAnalyzer.FetchTradesWorld(i, world)))

        for ret in rets do
            match ret with
            | Error str -> sw.WriteLine("找不到物品{0}，请尝试#is {0}", str)
            | Ok ma ->
                match ma with
                | Error exn -> sw.WriteLine("服务器处理失败，请稍后重试，错误信息： {0}", exn.Message)
                | Ok ma when ma.IsEmpty -> tt.AddRow(ma.ItemRecord.Name, "无记录", "--", "--", "--")
                | Ok ma ->
                    tt.AddRow(ma.ItemRecord.Name, ma.StdEvPrice(), ma.MinPrice(), ma.MaxPrice(), ma.LastUpdateTime())
        sw.Write(tt.ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("market", "查询市场订单", "物品Id或全名...")>]
    member x.HandleMarket(msgArg : CommandArgs) =
        let sw = new IO.StringWriter()
        let (succ, world, args) = CommandUtils.GetWorldWithDefault(msgArg.Arguments)
        if succ then sw.WriteLine("服务器：{0}", world.WorldName)
        else sw.WriteLine("默认服务器：{0}", world.WorldName)
        let tt = TextTable.FromHeader([| "名称"; "总体价格"; "HQ价格"; "更新时间" |])

        let rets =
            args
            |> Array.map (strToItemResult >> Result.map (fun i -> MarketUtils.MarketAnalyzer.FetchOrdersWorld(i, world)))

        for ret in rets do
            match ret with
            | Error str -> sw.WriteLine("找不到物品{0}，请尝试#is {0}", str)
            | Ok ma ->
                match ma with
                | Error exn -> sw.WriteLine("服务器处理失败，请稍后重试，错误信息： {0}", exn.Message)
                | Ok ma when ma.IsEmpty -> tt.AddRow(ma.ItemRecord.Name, "无记录", "--", "--")
                | Ok ma ->
                    let all = ma.TakeVolume(25).StdEvPrice()
                    let hq = ma.TakeHQ().TakeVolume(25).StdEvPrice()
                    tt.AddRow(tryLookupNpcPrice (ma.ItemRecord), all, hq, ma.LastUpdateTime())
        sw.Write(tt.ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("alltradelog", "查询全服交易记录", "物品Id或全名...")>]
    member x.HandleTradelogCrossWorld(msgArg : CommandArgs) =
        let sw = new IO.StringWriter()
        let tt = TextTable.FromHeader([| "名称"; "土豆"; "平均"; "低"; "高"; "最后成交" |])

        let rets =
            msgArg.Arguments
            |> Array.map (strToItemResult >> Result.map (fun i -> MarketUtils.MarketAnalyzer.FetchTradesAllWorld(i)))

        for ret in rets do
            match ret with
            | Error str -> sw.WriteLine("找不到物品{0}，请尝试#is {0}", str)
            | Ok ma ->
                match ma with
                | Error exn -> sw.WriteLine("服务器处理失败，请稍后重试，错误信息： {0}", exn.Message)
                | Ok wma ->
                    for (world, ma) in wma do
                        if ma.IsEmpty then tt.AddRow(ma.ItemRecord.Name, world.WorldName, "无记录", "--", "--", "--")
                        else
                            tt.AddRow
                                (ma.ItemRecord.Name, world.WorldName, ma.StdEvPrice(), ma.MinPrice(), ma.MaxPrice(),
                                 ma.LastUpdateTime())
        sw.Write(tt.ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("allmarket", "查询全服市场订单", "物品Id或全名...")>]
    member x.HandleMarketCrossWorld(msgArg : CommandArgs) =
        let sw = new IO.StringWriter()
        let tt = TextTable.FromHeader([| "名称"; "土豆"; "价格"; "低"; "更新时间" |])

        let rets =
            msgArg.Arguments
            |> Array.map (strToItemResult >> Result.map (fun i -> MarketUtils.MarketAnalyzer.FetchOrdersAllWorld(i)))

        for ret in rets do
            match ret with
            | Error str -> sw.WriteLine("找不到物品{0}，请尝试#is {0}", str)
            | Ok ma ->
                match ma with
                | Error exn -> sw.WriteLine("服务器处理失败，请稍后重试，错误信息： {0}", exn.Message)
                | Ok wma ->
                    for (world, ma) in wma do
                        if ma.IsEmpty then
                            tt.AddRow(tryLookupNpcPrice (ma.ItemRecord), world.WorldName, "无记录", "--", "--")
                        else
                            let ma = ma.TakeVolume(cutoff)
                            tt.AddRow
                                (tryLookupNpcPrice (ma.ItemRecord), world.WorldName, ma.StdEvPrice(), ma.MinPrice(),
                                 ma.LastUpdateTime())
        sw.Write(tt.ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("is", "查找名字包含字符的物品", "关键词...")>]
    member x.HandleItemSearch(msgArg : CommandArgs) =
        let tt = TextTable.FromHeader([| "查询"; "Id"; "物品" |])
        for i in msgArg.Arguments do
            if isNumber (i) then
                let ret = itemCol.TryLookupById(i |> int32)
                if ret.IsSome then tt.AddRow(i, ret.Value.Name, ret.Value.Id.ToString())
            else
                let ret = itemCol.SearchByName(i) |> Array.sortBy (fun x -> x.Id)
                if ret.Length = 0 then
                    tt.AddRow(i, "无", "无")
                else
                    for item in ret do
                        tt.AddRow(i, item.Id.ToString(), item.Name)

        msgArg.CqEventArgs.QuickMessageReply(tt.ToString())

    [<CommandHandlerMethodAttribute("r", "根据表达式汇总多个物品的材料，不查询价格\r\n大于50数字视为物品ID", "")>]
    member x.HandleRecipeSumExpr(msgArg : CommandArgs) =
        let sw = new IO.StringWriter()
        let acc = XivExpression.ItemAccumulator()
        let parser = XivExpression.XivExpression()
        for str in msgArg.Arguments do
            match parser.TryEval(str) with
            | Error err -> sw.WriteLine("对{0}求值时发生错误\r\n{1}", str, err.Message)
            | Ok(XivExpression.XivOperand.Number i) -> sw.WriteLine("{0} 的返回值为数字 : {1}", str, i)
            | Ok(XivExpression.XivOperand.Accumulator a) ->
                for kv in a do
                    let (item, runs) = kv.Key, kv.Value
                    let recipe = rm.GetMaterialsOne(item)
                    if recipe.Length = 0 then
                        sw.WriteLine("{0} 没有生产配方", item.Name)
                    else
                        for (i, r) in recipe do
                            acc.AddOrUpdate(i, r * runs)
        let tt = TextTable.FromHeader([| "物品"; "数量" |])

        let final =
            acc
            |> Seq.toArray
            |> Array.map (fun x -> (x.Key, x.Value))
            |> Array.sortBy (fun (i, _) -> i.Id)
        for (item, amount) in final do
            tt.AddRow(item.Name, amount)
        sw.Write(tt.ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("rr", "根据表达式汇总多个物品的基础材料，不查询价格\r\n大于50数字视为物品ID", "")>]
    member x.HandleRecipeSumExprRec(msgArg : CommandArgs) =
        let sw = new IO.StringWriter()
        let acc = XivExpression.ItemAccumulator()
        let parser = XivExpression.XivExpression()
        for str in msgArg.Arguments do
            match parser.TryEval(str) with
            | Error err -> sw.WriteLine("对{0}求值时发生错误\r\n{1}", str, err.Message)
            | Ok(XivExpression.XivOperand.Number i) -> sw.WriteLine("{0} 的返回值为数字 : {1}", str, i)
            | Ok(XivExpression.XivOperand.Accumulator a) ->
                for kv in a do
                    let (item, runs) = kv.Key, kv.Value
                    let recipe = rm.GetMaterialsRec(item)
                    if recipe.Length = 0 then
                        sw.WriteLine("{0} 没有生产配方", item.Name)
                    else
                        for (i, r) in recipe do
                            acc.AddOrUpdate(i, r * runs)
        let tt = TextTable.FromHeader([| "物品"; "数量" |])

        let final =
            acc
            |> Seq.toArray
            |> Array.map (fun x -> (x.Key, x.Value))
            |> Array.sortBy (fun (i, _) -> i.Id)
        for (item, amount) in final do
            tt.AddRow(item.Name, amount)
        sw.Write(tt.ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("rc", "计算物品基础材料成本（不支持表达式）", "物品Id或全名...")>]
    member x.HandleItemFinalRecipeExpr(msgArg : CommandArgs) =
        let sw = new IO.StringWriter()
        let (succ, world, args) = CommandUtils.GetWorldWithDefault(msgArg.Arguments)
        if succ then sw.WriteLine("服务器：{0}", world.WorldName)
        else sw.WriteLine("默认服务器：{0}", world.WorldName)
        let acc = XivExpression.ItemAccumulator()
        let parser = XivExpression.XivExpression()

        for str in args do
            match parser.TryEval(str) with
            | Error err -> sw.WriteLine("对{0}求值时发生错误\r\n{1}", str, err.Message)
            | Ok(XivExpression.XivOperand.Number i) -> sw.WriteLine("{0} 的返回值为数字 : {1}", str, i)
            | Ok(XivExpression.XivOperand.Accumulator a) ->
                for kv in a do
                    let (item, runs) = kv.Key, kv.Value
                    let recipe = rm.GetMaterialsOne(item)
                    if recipe.Length = 0 then
                        sw.WriteLine("{0} 没有生产配方", item.Name)
                    else
                        for (i, r) in recipe do
                            acc.AddOrUpdate(i, r * runs)

        let final =
            acc
            |> Seq.toArray
            |> Array.map (fun x -> (x.Key, MarketUtils.MarketAnalyzer.FetchTradesWorld(x.Key, world), x.Value))
            |> Array.sortBy (fun (i, _, _) -> i.Id)

        let mutable sum = MarketUtils.StdEv.Zero
        let tt = TextTable.FromHeader([| "物品"; "价格"; "需求"; "总价"; "更新时间" |])
        for (item, ma, count) in final do
            match ma with
            | Error err -> sw.WriteLine("查询{0}时发生错误：{1}", item.Name, err.Message)
            | Ok ma when ma.IsEmpty -> tt.AddRow(tryLookupNpcPrice (ma.ItemRecord), "无记录", "--", "--", "--")
            | Ok ma ->
                let std = ma.StdEvPrice()
                let total = std * count
                sum <- sum + total
                tt.AddRow(tryLookupNpcPrice (ma.ItemRecord), ma.StdEvPrice(), count, total, ma.LastUpdateTime())
        tt.AddRow("总计", sum, "--", "--", "--")
        sw.Write(tt.ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("rrc", "计算物品基础材料成本（不支持表达式）", "物品Id或全名...")>]
    member x.HandleItemFinalRecipeRecExpr(msgArg : CommandArgs) =
        let sw = new IO.StringWriter()
        let (succ, world, args) = CommandUtils.GetWorldWithDefault(msgArg.Arguments)
        if succ then sw.WriteLine("服务器：{0}", world.WorldName)
        else sw.WriteLine("默认服务器：{0}", world.WorldName)
        let acc = XivExpression.ItemAccumulator()
        let parser = XivExpression.XivExpression()

        for str in args do
            match parser.TryEval(str) with
            | Error err -> sw.WriteLine("对{0}求值时发生错误\r\n{1}", str, err.Message)
            | Ok(XivExpression.XivOperand.Number i) -> sw.WriteLine("{0} 的返回值为数字 : {1}", str, i)
            | Ok(XivExpression.XivOperand.Accumulator a) ->
                for kv in a do
                    let (item, runs) = kv.Key, kv.Value
                    let recipe = rm.GetMaterialsRec(item)
                    if recipe.Length = 0 then
                        sw.WriteLine("{0} 没有生产配方", item.Name)
                    else
                        for (i, r) in recipe do
                            acc.AddOrUpdate(i, r * runs)

        let final =
            acc
            |> Seq.toArray
            |> Array.map (fun x -> (x.Key, MarketUtils.MarketAnalyzer.FetchTradesWorld(x.Key, world), x.Value))
            |> Array.sortBy (fun (i, _, _) -> i.Id)

        let mutable sum = MarketUtils.StdEv.Zero
        let tt = TextTable.FromHeader([| "物品"; "价格(前25%订单)"; "需求"; "总价"; "更新时间" |])
        for (item, ma, count) in final do
            match ma with
            | Error err -> sw.WriteLine("查询{0}时发生错误：{1}", item.Name, err.Message)
            | Ok ma when ma.IsEmpty -> tt.AddRow(tryLookupNpcPrice (ma.ItemRecord), "无记录", "--", "--", "--")
            | Ok ma ->
                let std = ma.StdEvPrice()
                let total = std * count
                sum <- sum + total
                tt.AddRow(tryLookupNpcPrice (ma.ItemRecord), ma.StdEvPrice(), count, total, ma.LastUpdateTime())
        tt.AddRow("总计", sum, "--", "--", "--")
        sw.Write(tt.ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("ssc", "计算部分道具兑换的价格", "兑换所需道具的名称或ID，只处理1个")>]
    member x.HandleSSS(msgArg : CommandArgs) =
        let sw = new IO.StringWriter()
        let (succ, world, args) = CommandUtils.GetWorldWithDefault(msgArg.Arguments)
        if succ then sw.WriteLine("服务器：{0}", world.WorldName)
        else sw.WriteLine("默认服务器：{0}", world.WorldName)
        if args.Length = 0 then failwithf "参数不足"
        let item = args.[0]
        let ret = SpecialShop.SpecialShopCollection.Instance.TrySearchByName(item)
        if ret.IsSome then
            let tt = TextTable.FromHeader([| "道具"; "名称"; "价格(前25%订单)"; "低"; "单道具价值"; "更新时间" |])
            for info in ret.Value do
                let i = itemCol.LookupById(info.ReceiveItem)
                let ret = MarketUtils.MarketAnalyzer.FetchOrdersWorld(i, world)
                match ret with
                | Ok x when x.IsEmpty -> tt.AddRow(item, i.Name, "无记录", "--", "--", "--")
                | Ok ret ->
                    let ret = ret.TakeVolume(25)
                    let stdev = ret.StdEvPrice()
                    let low = ret.MinPrice()
                    let upd = ret.LastUpdateTime()

                    let v = stdev * (info.ReceiveCount |> float) / (info.CostCount |> float)
                    tt.AddRow(item, tryLookupNpcPrice (i), stdev, low, v, upd)
                | Error err -> sw.WriteLine("{0} 服务器处理失败，请稍后重试", i.Name)
            sw.Write(tt.ToString())
            msgArg.CqEventArgs.QuickMessageReply(sw.ToString())
        else
            msgArg.CqEventArgs.QuickMessageReply(sprintf "%s 不能兑换道具" item)

    [<CommandHandlerMethodAttribute("mentor", "今日导随运势", "")>]
    member x.HandleMentor(msgArg : CommandArgs) =
        let sw = new IO.StringWriter()
        let dicer = Dicer(SeedOption.SeedByUserDay(msgArg.MessageEvent))

        let fortune, events =
            match dicer.GetRandom(100u) with
            | x when x <= 5 -> MentorUtils.fortune.[0]
            | x when x <= 20 -> MentorUtils.fortune.[1]
            | x when x <= 80 -> MentorUtils.fortune.[2]
            | x when x <= 95 -> MentorUtils.fortune.[3]
            | _ -> MentorUtils.fortune.[4]

        let event = dicer.GetRandomItem(events)
        sw.WriteLine("{0} 今日导随运势为：", msgArg.MessageEvent.GetNicknameOrCard)
        sw.WriteLine("{0} : {1}", fortune, event)

        let s, a =
            let count = MentorUtils.shouldOrAvoid.Count() |> uint32
            let idx = dicer.GetRandomArray(count + 1u, 3 * 2)
            let a = idx |> Array.map (fun id -> MentorUtils.shouldOrAvoid.[id].Value.Value)
            a.[0..2], a.[3..]
        sw.WriteLine("宜：{0}", String.concat "/" s)
        sw.WriteLine("忌：{0}", String.concat "/" a)
        let c, jobs = dicer.GetRandomItem(MentorUtils.classJob)
        let job = dicer.GetRandomItem(jobs)
        sw.WriteLine("推荐职业: {0} {1}", c, job)
        let location =
            let count = MentorUtils.location.Count() |> uint32
            let idx = dicer.GetRandom(count + 1u)
            MentorUtils.location.[idx].Value
        sw.WriteLine("推荐排本场所: {0}", location.Value)
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())
