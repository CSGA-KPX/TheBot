namespace TheBot.Module.XivModule
open System
open KPX.FsCqHttp.Handler.CommandHandlerBase
open LibDmfXiv.Client
open XivData
open TheBot.Module.XivModule.Utils
open TheBot.Utils

type XivModule() = 
    inherit CommandHandlerBase()
    let rm       = Recipe.RecipeManager.GetInstance()
    let cutoff   = 25
    let itemCol  = Item.ItemCollection.Instance

    let isNumber(str : string) = 
        if str.Length <> 0 then
            String.forall (Char.IsDigit) str
        else
            false
    let strToItem(str : string)= 
        if isNumber(str) then
            itemCol.LookupById(Convert.ToInt32(str))
        else
            itemCol.LookupByName(str)

    [<CommandHandlerMethodAttribute("tradelog", "查询交易记录", "物品Id或全名...")>]
    member x.HandleTradelog(msgArg : CommandArgs) = 
        let sw = new IO.StringWriter()
        let (succ, world, args) = CommandUtils.GetWorldWithDefault(msgArg.Arguments)
        if succ then
            sw.WriteLine("服务器：{0}", world.WorldName)
        else
            sw.WriteLine("默认服务器：{0}", world.WorldName)
        let tt = TextTable.FromHeader([|"名称"; "平均"; "低"; "高"; "更新时间"|])
        for i in args do 
            match strToItem(i) with
            | None -> sw.WriteLine("找不到物品{0}，请尝试#is {0}", i)
            | Some(i) ->
                let ret = 
                    let itemId = i.Id |> uint32
                    async {
                        let worldId = world.WorldId // 必须在代码引用之外处理为简单类型
                        return! TradeLog.TradelogProxy.callSafely <@ fun server -> server.GetByIdWorld worldId itemId 20 @>
                    } |> Async.RunSynchronously
                match ret with
                | Ok logs when logs.Length = 0 ->
                    sw.WriteLine("{0} 无记录", i.Name)
                | Ok o ->
                    let stdev= MarketUtils.GetStdEvTrade(o)
                    let low  = o |> Array.map (fun item -> item.Price) |> Array.min
                    let high = o |> Array.map (fun item -> item.Price) |> Array.max
                    let upd  = 
                        let max = o |> Array.maxBy (fun item -> item.TimeStamp)
                        max.GetHumanReadableTimeSpan()
                    tt.AddRow(i.Name, stdev, low, high, upd)
                | Error err ->
                    sw.WriteLine("{0} 服务器处理失败，请稍后重试", i.Name)
        sw.Write(tt.ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("market", "查询市场订单", "物品Id或全名...")>]
    member x.HandleMarket(msgArg : CommandArgs) = 
        let sw = new IO.StringWriter()
        let (succ, world, args) = CommandUtils.GetWorldWithDefault(msgArg.Arguments)
        if succ then
            sw.WriteLine("服务器：{0}", world.WorldName)
        else
            sw.WriteLine("默认服务器：{0}", world.WorldName)
        let tt = TextTable.FromHeader([|"名称"; "价格(前25%订单)"; "低"; "更新时间"|])
        for i in args do 
            match strToItem(i) with
            | None -> sw.WriteLine("找不到物品{0}，请尝试#is {0}", i)
            | Some(i) ->
                let ret =
                    let itemId = i.Id |> uint32
                    async {
                        let worldId = world.WorldId // 必须在代码引用之外处理为简单类型
                        return! MarketOrder.MarketOrderProxy.callSafely <@ fun server -> server.GetByIdWorld worldId itemId @>
                    } |> Async.RunSynchronously
                match ret with
                | Ok x when x.Orders.Length = 0 ->
                    sw.WriteLine("{0} 无记录", i.Name)
                | Ok ret ->
                    let o = ret.Orders
                    let stdev= MarketUtils.GetStdEvMarket(o, 25)
                    let low  = o |> Array.map (fun item -> item.Price) |> Array.min
                    let upd  = ret.GetHumanReadableTimeSpan()
                    tt.AddRow(i.Name, stdev, low, upd)
                | Error err ->
                    sw.WriteLine("{0} 服务器处理失败，请稍后重试", i.Name)
        sw.Write(tt.ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())
    
    [<CommandHandlerMethodAttribute("alltradelog", "查询全服交易记录", "物品Id或全名...")>]
    member x.HandleTradelogCrossWorld(msgArg : CommandArgs) = 
        let sw = new IO.StringWriter()
        let tt = TextTable.FromHeader([|"名称"; "土豆"; "平均"; "低"; "高"; "最后成交"|])
        for i in msgArg.Arguments do 
            match strToItem(i) with
            | None -> sw.WriteLine("找不到物品{0}，请尝试#is {0}", i)
            | Some(i) ->
                let ret = 
                    let itemId = i.Id |> uint32
                    async {
                        return! TradeLog.TradelogProxy.callSafely <@ fun server -> server.GetByIdAllWorld itemId 20 @>
                    } |> Async.RunSynchronously
                match ret with
                | Ok logs when logs.Length = 0 ->
                    sw.WriteLine("{0} 无记录", i.Name)
                | Ok all ->
                    let grouped = all |> Array.groupBy (fun x -> x.WorldId)
                    for worldId, o in grouped do 
                        let stdev= MarketUtils.GetStdEvTrade(o)
                        let world = World.WorldFromId.[worldId]
                        let low  = o |> Array.map (fun item -> item.Price) |> Array.min
                        let high = o |> Array.map (fun item -> item.Price) |> Array.max
                        let upd  = 
                            let max = o |> Array.maxBy (fun item -> item.TimeStamp)
                            max.GetHumanReadableTimeSpan()
                        tt.AddRow(i.Name, world.WorldName,  stdev, low, high, upd)
                | Error err ->
                    sw.WriteLine("{0} 服务器处理失败，请稍后重试", i.Name)
        sw.Write(tt.ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("allmarket", "查询全服市场订单", "物品Id或全名...")>]
    member x.HandleMarketCrossWorld(msgArg : CommandArgs) =  
        let sw = new IO.StringWriter()
        let tt = TextTable.FromHeader([|"名称"; "土豆"; "价格(前25%订单)"; "低"; "更新时间"|])
        for i in msgArg.Arguments do 
            match strToItem(i) with
            | None -> sw.WriteLine("找不到物品{0}，请尝试#is {0}", i)
            | Some(i) ->
                let ret =
                    let itemId = i.Id |> uint32
                    async {
                        return! MarketOrder.MarketOrderProxy.callSafely <@ fun server -> server.GetByIdAllWorld itemId @>
                    } |> Async.RunSynchronously
                match ret with
                | Ok ret when ret.Length = 0 ->
                     sw.WriteLine("{0} 无记录", i.Name)
                | Ok ret ->
                    for r in ret do 
                        let server  = World.WorldFromId.[r.WorldId]
                        let o = r.Orders
                        let stdev= MarketUtils.GetStdEvMarket(o, 25)
                        let low  = o |> Array.map (fun item -> item.Price) |> Array.min
                        let upd  = r.GetHumanReadableTimeSpan()
                        tt.AddRow(i.Name, server.WorldName,  stdev, low, upd)
                | Error err ->
                    sw.WriteLine("{0} 服务器处理失败，请稍后重试", i.Name)
        sw.Write(tt.ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("is", "查找名字包含字符的物品", "关键词...")>]
    member x.HandleItemSearch(msgArg : CommandArgs) = 
        let tt = TextTable.FromHeader([|"查询"; "物品"; "Id"|])
        for i in msgArg.Arguments do 
            let ret =
                itemCol.SearchByName(i)
                |> Array.sortBy (fun x -> x.Id)
            if ret.Length = 0 then
                tt.AddRow(i, "无", "无")
            else
                for item in ret do 
                    tt.AddRow(i, item.Name, item.Id)

        msgArg.CqEventArgs.QuickMessageReply(tt.ToString())

    [<CommandHandlerMethodAttribute("r", "根据表达式汇总多个物品的材料，不查询价格\r\n大于50数字视为物品ID", "")>]
    member x.HandleRecipeSumExpr(msgArg : CommandArgs) = 
        let sw = new IO.StringWriter()
        let acc = new XivExpression.Accumulator()
        let parser = new XivExpression.XivExpression()
        for str in msgArg.Arguments do 
            match parser.TryEval(str) with
            | Error err ->
                sw.WriteLine("对{0}求值时发生错误\r\n{1}", str, err.Message)
            | Ok (XivExpression.XivOperand.Number i) ->
                sw.WriteLine("{0} 的返回值为数字 : {1}", str, i)
            | Ok (XivExpression.XivOperand.Accumulator a) ->
                for kv in a do
                    let (item, runs) = kv.Key, kv.Value
                    let recipe = rm.GetMaterialsOne(item)
                    if recipe.Length = 0 then
                        sw.WriteLine("{0} 没有生产配方", item.Name)
                    else
                        for (i, r) in recipe do 
                            acc.AddOrUpdate(i, r * runs)
        let tt = TextTable.FromHeader([|"物品"; "数量"|])
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
        let acc = new XivExpression.Accumulator()
        let parser = new XivExpression.XivExpression()
        for str in msgArg.Arguments do 
            match parser.TryEval(str) with
            | Error err ->
                sw.WriteLine("对{0}求值时发生错误\r\n{1}", str, err.Message)
            | Ok (XivExpression.XivOperand.Number i) ->
                sw.WriteLine("{0} 的返回值为数字 : {1}", str, i)
            | Ok (XivExpression.XivOperand.Accumulator a) ->
                for kv in a do
                    let (item, runs) = kv.Key, kv.Value
                    let recipe = rm.GetMaterialsRec(item)
                    if recipe.Length = 0 then
                        sw.WriteLine("{0} 没有生产配方", item.Name)
                    else
                        for (i, r) in recipe do 
                            acc.AddOrUpdate(i, r * runs)
        let tt = TextTable.FromHeader([|"物品"; "数量"|])
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
    member x.HandleItemFinalRecipe(msgArg : CommandArgs) = 
        let sw = new IO.StringWriter()
        let (succ, world, args) = CommandUtils.GetWorldWithDefault(msgArg.Arguments)
        if succ then
            sw.WriteLine("服务器：{0}", world.WorldName)
        else
            sw.WriteLine("默认服务器：{0}", world.WorldName)
        let tt = TextTable.FromHeader([|"查询"; "物品"; "价格(前25%订单)"; "需求"; "总价"; "更新时间"|])
        for i in args do 
            match strToItem(i) with
            | None -> sw.WriteLine("找不到物品{0}，请尝试#is {0}", i)
            | Some(i) ->
                let recipe = rm.GetMaterialsOne(i)
                let mutable sum = MarketUtils.StdEv.Zero
                for (item, count) in recipe |> Array.sortBy (fun x -> (fst x).Id) do 
                    let ret = 
                        let itemId = item.Id |> uint32
                        async {
                            let worldId = world.WorldId
                            return! MarketOrder.MarketOrderProxy.call <@ fun server -> server.GetByIdWorld worldId itemId @>
                        } |> Async.RunSynchronously
                    if ret.Orders.Length <> 0 then
                        let price = MarketUtils.GetStdEvMarket(ret.Orders, cutoff)
                        let total = price * count
                        sum <- sum + total
                        tt.AddRow(i.Name, item.Name, price, count, total, ret.GetHumanReadableTimeSpan())
                    else
                        tt.AddRow(i.Name, item.Name, "无记录", "--", "--", "--")
                tt.AddRow(i.Name, "总计", sum, "--", "--", "--")
        sw.Write(tt.ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("rrc", "计算物品基础材料成本（不支持表达式）", "物品Id或全名...")>]
    member x.HandleItemFinalRecipeRec(msgArg : CommandArgs) = 
        let sw = new IO.StringWriter()
        let (succ, world, args) = CommandUtils.GetWorldWithDefault(msgArg.Arguments)
        if succ then
            sw.WriteLine("服务器：{0}", world.WorldName)
        else
            sw.WriteLine("默认服务器：{0}", world.WorldName)
        let tt = TextTable.FromHeader([|"查询"; "物品"; "价格(前25%订单)"; "需求"; "总价"; "更新时间"|])
        for i in args do 
            match strToItem(i) with
            | None -> sw.WriteLine("找不到物品{0}，请尝试#is {0}", i)
            | Some(i) ->
                let recipe = rm.GetMaterialsRec(i)
                let mutable sum = MarketUtils.StdEv.Zero
                for (item, count) in recipe |> Array.sortBy (fun x -> (fst x).Id) do 
                    let ret = 
                        let itemId = item.Id |> uint32
                        async {
                            let worldId = world.WorldId
                            return! MarketOrder.MarketOrderProxy.call <@ fun server -> server.GetByIdWorld worldId itemId @>
                        } |> Async.RunSynchronously
                    if ret.Orders.Length <> 0 then
                        let price = MarketUtils.GetStdEvMarket(ret.Orders, cutoff)
                        let total = price * count
                        sum <- sum + total
                        tt.AddRow(i.Name, item.Name, price, count, total, ret.GetHumanReadableTimeSpan())
                    else
                        tt.AddRow(i.Name, item.Name, "无记录", "--", "--", "--")
                tt.AddRow(i.Name, "总计", sum, "--", "--", "--")
        sw.Write(tt.ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("ssc", "计算部分道具兑换的价格", "兑换所需道具的名称或ID，只处理1个")>]
    member x.HandleSSS(msgArg : CommandArgs) = 
        let sw = new IO.StringWriter()
        let (succ, world, args) = CommandUtils.GetWorldWithDefault(msgArg.Arguments)
        if succ then
            sw.WriteLine("服务器：{0}", world.WorldName)
        else
            sw.WriteLine("默认服务器：{0}", world.WorldName)
        if args.Length = 0 then failwithf "参数不足"
        let item = args.[0]
        let ret = SpecialShop.SpecialShopCollection.Instance.LookupByName(item)
        if ret.IsSome then
            let tt = TextTable.FromHeader([|"道具"; "名称"; "价格(前25%订单)"; "低"; "单道具价值"; "更新时间"|])
            for info in ret.Value do 
                let i = itemCol.LookupById(info.ReceiveItem).Value
                let ret =
                    let itemId = info.ReceiveItem |> uint32
                    async {
                        let worldId = world.WorldId // 必须在代码引用之外处理为简单类型
                        return! MarketOrder.MarketOrderProxy.callSafely <@ fun server -> server.GetByIdWorld worldId itemId @>
                    } |> Async.RunSynchronously
                match ret with
                | Ok x when x.Orders.Length = 0 ->
                    tt.AddRow(item, i.Name, "无记录", "--", "--", "--")
                | Ok ret ->
                    let o = ret.Orders
                    let stdev= MarketUtils.GetStdEvMarket(o, 25)
                    let low  = o |> Array.map (fun item -> item.Price) |> Array.min
                    let upd  = ret.GetHumanReadableTimeSpan()

                    let v = stdev * (info.ReceiveCount |> float) / (info.CostCount |> float)
                    tt.AddRow(item, i.Name, stdev, low, v, upd)
                | Error err ->
                    sw.WriteLine("{0} 服务器处理失败，请稍后重试", i.Name)
            sw.Write(tt.ToString())
            msgArg.CqEventArgs.QuickMessageReply(sw.ToString())
        else
            msgArg.CqEventArgs.QuickMessageReply(sprintf "%s 不能兑换道具" item)

    [<CommandHandlerMethodAttribute("mentor", "今日导随运势", "")>]
    member x.HandleMentor(msgArg : CommandArgs)= 
        let sw = new IO.StringWriter()
        let dicer = new Dicer(SeedOption.SeedByUserDay(msgArg.MessageEvent))

        let fortune, events = 
            match dicer.GetRandom(100u) with
            | x when x <= 5  -> MentorUtils.fortune.[0]
            | x when x <= 20 -> MentorUtils.fortune.[1]
            | x when x <= 80 -> MentorUtils.fortune.[2]
            | x when x <= 95 -> MentorUtils.fortune.[3]
            | _              -> MentorUtils.fortune.[4]
        let event = dicer.GetRandomItem(events)
        sw.WriteLine("{0} 今日导随运势为：", msgArg.MessageEvent.GetNicknameOrCard)
        sw.WriteLine("{0} : {1}", fortune, event)

        let s,a   = 
            let count = MentorUtils.shouldOrAvoid.Count |> uint32
            let idx   = dicer.GetRandomArray(count + 1u, 3*2)
            let a = idx |> Array.map (fun id -> MentorUtils.shouldOrAvoid.[id].Value)
            a.[0..2], a.[3..]
        sw.WriteLine("宜：{0}", String.concat "/" s)
        sw.WriteLine("忌：{0}", String.concat "/" a)
        let c, jobs = dicer.GetRandomItem(MentorUtils.classJob)
        let job = dicer.GetRandomItem(jobs)
        sw.WriteLine("推荐职业: {0} {1}", c, job)
        let location = 
            let count = MentorUtils.location.Count |> uint32
            let idx = dicer.GetRandom(count + 1u)
            MentorUtils.location.[idx].Value
        sw.WriteLine("推荐排本场所: {0}", location)
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())