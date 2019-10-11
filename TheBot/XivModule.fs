module XivModule
open System
open KPX.FsCqHttp.Handler.CommandHandlerBase
open LibDmfXiv
open LibDmfXiv.Client
open XivData

module MarketUtils = 
    let internal TakeMarketSample (samples : Shared.MarketOrder.FableMarketOrder [] , cutPct : int) = 
        [|
            //(price, count)
            let samples = samples |> Array.sortBy (fun x -> x.Price)
            let itemCount = samples |> Array.sumBy (fun x -> x.Count |> int)
            let cutLen = itemCount * cutPct / 100
            let mutable rest = cutLen
            match itemCount = 0 , cutLen = 0 with
            | true, _ -> ()
            | false, true ->
                yield ((int) samples.[0].Price, 1)
            | false, false ->
                for record in samples do
                    let takeCount = min rest (record.Count |> int)
                    if takeCount <> 0 then
                        rest <- rest - takeCount
                        yield ((int) record.Price, takeCount)
        |]

    type StdEv = 
        {
            Average   : float
            Deviation : float
        }
        override x.ToString() = 
            String.Format("{0:n0}±{1:n0}", x.Average, x.Deviation)

        static member (*) (x : StdEv, y : float) = 
            {
                Average   = x.Average * y
                Deviation = x.Deviation * y
            }

        static member (+) (x : StdEv, y : StdEv) = 
            {
                Average   = x.Average + y.Average
                Deviation = x.Deviation + y.Deviation
            }

        static member Zero = 
            {
                Average   = 0.0
                Deviation = 0.0
            }

        static member FromData(data : float []) = 
            let avg = Array.average data
            let sum = data |> Array.sumBy (fun x -> (x - avg) ** 2.0)
            let ev  = sum / (float data.Length)
            { Average = avg; Deviation = sqrt ev }

    let GetStdEvMarket(market : Shared.MarketOrder.FableMarketOrder [] , cutPct : int) = 
        let samples = TakeMarketSample(market, cutPct)
        let itemCount = samples |> Array.sumBy (fun (a, b) -> (float) b)
        let average = 
            let priceSum = samples |> Array.sumBy (fun (a, b) -> (float) (a * b))
            priceSum / itemCount
        let sum = 
            samples
            |> Array.sumBy (fun (a, b) -> (float b) * (( (float a) - average) ** 2.0) )
        let ev  = sum / itemCount
        { Average = average; Deviation = sqrt ev }

    let GetStdEvTrade(tradelog : Shared.TradeLog.FableTradeLog []) = 
        let samples = tradelog |> Array.map (fun x -> (x.Price, x.Count))
        let itemCount = samples |> Array.sumBy (fun (a, b) -> (float) b)
        let average = 
            let priceSum = samples |> Array.sumBy (fun (a, b) -> (float) (a * b))
            priceSum / itemCount
        let sum = 
            samples
            |> Array.sumBy (fun (a, b) -> (float b) * (( (float a) - average) ** 2.0) )
        let ev  = sum / itemCount
        { Average = average; Deviation = sqrt ev }

(*
module MentorUtils = 
    let fortune = 
        [|
            "大吉", "行会令连送/三导师高铁四人本/假风火土白给".Split('/')
            "小吉", "豆芽已看攻略/稳定7拖1".Split('/')
            "平"  , "听话懂事初见/不急不缓四人本/超越之力25%".Split('/')
            "小凶", "塔三团灭/遇假火/260T白山堡".Split('/')
            "大凶", "嘴臭椰新/装会假火/极神小龙虾".Split('/')
        |]
    let shouldOrAvoid = 
        [|
            yield! "中途参战，红色划水，蓝色carry，绿色擦屁股，辱骂毒豆芽，辱骂假火，副职导随".Split('，')
            let mentor = 
                ContentFinderCondition.ContentFinderCondition
                |> Seq.filter (fun x -> x.IsMentor)
            yield! 
                mentor
                |> Seq.map (fun x -> x.Name)
                |> Seq.toArray
            yield!
                mentor
                |> Seq.map (fun x -> x.ContentType)
                |> Seq.distinct
                |> Seq.toArray
        |]
    let classJob = 
        [|
            "红", "近战，远敏，复活机，法系".Split('，')
            "绿", "崩石怪，小仙女，游戏王".Split('，')
            "蓝", "裂石飞环，神圣领域，暗技人".Split('，')
        |]
    let allowedLocation = Collections.Generic.HashSet<byte>([|0uy; 1uy; 2uy; 6uy; 13uy; 14uy; 15uy;|])

    let location = 
        TerritoryType.AllTerritory
        |> Seq.filter (fun x -> allowedLocation.Contains(x.TerritoryIntendedUse))
        |> Seq.distinctBy (fun x -> x.ToString())
        |> Array.ofSeq
*)

module CommandUtils =
    /// 拉诺西亚
    let defaultServer = World.WorldFromId.[1042us]

    /// 扫描参数，查找第一个服务器名
    /// 如果没找到，返回None
    let tryGetWorld (a : string []) = 
        let (w, args) =
            a
            |> Array.partition (fun x -> World.WorldFromName.ContainsKey(x))

        let world = 
            if w.Length = 0 then
                None
            else
                Some World.WorldFromName.[ w.[0] ]

        world, args

    /// 扫描参数，查找第一个服务器名
    /// 成功返回 true 服务器，失败返回 false 默认服务器
    let GetWorldWithDefault (a : string []) =
        match tryGetWorld(a) with
        | Some x, args -> (true, x, args)
        | None , args-> (false, defaultServer, args)

module XivExpression = 
    open System.Text.RegularExpressions
    open GenericRPN

    let t = new Collections.Generic.HashSet<string>()

    type Accumulator() = 
        inherit Collections.Generic.Dictionary<Item.ItemRecord, float>()

        member x.AddOrUpdate(item, runs) = 
            if x.ContainsKey(item) then
                x.[item] <- x.[item] + runs
            else
                x.Add(item, runs)

        member x.MergeWith(a : Accumulator, ?isAdd : bool) = 
            let add = defaultArg isAdd true
            for kv in a do 
                if add then
                    x.AddOrUpdate(kv.Key, kv.Value)
                else
                    x.AddOrUpdate(kv.Key, -(kv.Value))
            x

        static member Singleton (item : Item.ItemRecord) = 
            let a = new Accumulator()
            a.Add(item, 1.0)
            a

    type XivOperand = 
        | Number of float
        | Accumulator of Accumulator

        interface IOperand<XivOperand> with
            override l.Add(r) = 
                match l, r with
                | (Number i1), (Number i2) ->
                    Number (i1 + i2)
                | (Accumulator a1), (Accumulator a2) ->
                    Accumulator (a1.MergeWith(a2))
                | (Number i), (Accumulator a) ->
                    raise <| InvalidOperationException("不允许材料和数字相加")
                | (Accumulator a), (Number i) ->
                    (r :> IOperand<XivOperand>).Add(l)
            override l.Sub(r) = 
                match l, r with
                | (Number i1), (Number i2) ->
                    Number (i1 - i2)
                | (Accumulator a1), (Accumulator a2) ->
                    Accumulator (a1.MergeWith(a2, false))
                | (Number i), (Accumulator a) ->
                     raise <| InvalidOperationException("不允许材料和数字相减")
                | (Accumulator a), (Number i) ->
                    (r :> IOperand<XivOperand>).Sub(l)
            override l.Mul(r) = 
                match l, r with
                | (Number i1), (Number i2) ->
                    Number (i1 * i2)
                | (Accumulator a1), (Accumulator a2) ->
                    raise <| InvalidOperationException("不允许材料和材料相乘")
                | (Number i), (Accumulator a) ->
                    let keys = a.Keys |> Seq.toArray
                    for k in keys do 
                        a.[k] <- a.[k] * i
                    Accumulator a
                | (Accumulator a), (Number i) ->
                    (r :> IOperand<XivOperand>).Mul(l)
            override l.Div(r) = 
                match l, r with
                | (Number i1), (Number i2) ->
                    Number (i1 / i2)
                | (Accumulator a1), (Accumulator a2) ->
                    raise <| InvalidOperationException("不允许材料和材料相减")
                | (Number i), (Accumulator a) ->
                    let keys = a.Keys |> Seq.toArray
                    for k in keys do 
                        a.[k] <- a.[k] / i
                    Accumulator a
                | (Accumulator a), (Number i) ->
                    (r :> IOperand<XivOperand>).Div(l)

    type XivExpression() = 
        inherit GenericRPNParser<XivOperand>()

        let itemKeyLimit = 50

        let tokenRegex = new Regex("([0-9]+|[\-+*/()])", RegexOptions.Compiled)

        override x.Tokenize(str) = 
            [|
                let strs = tokenRegex.Split(str) |> Array.filter (fun x -> x <> "")
                for str in strs do
                    match str with
                    | _ when Char.IsDigit(str.[0]) ->
                        let num = str |> int
                        if num >= itemKeyLimit then
                            let item = Item.ItemCollection.Instance.LookupById(num)
                            if item.IsNone then
                                failwithf ""
                            yield Operand (Accumulator (Accumulator.Singleton(item.Value)))
                        else
                            yield Operand (Number (num |> float))
                    | _ when x.Operatos.ContainsKey(str) -> 
                        yield Operator (x.Operatos.[str])
                    | _ -> 
                        let item = Item.ItemCollection.Instance.LookupByName(str)
                        if item.IsSome then
                            yield Operand (Accumulator (Accumulator.Singleton(item.Value)))
                        else
                            failwithf "Unknown token %s" str
            |]

        member x.Eval(str : string) = 
            let func = new EvalDelegate<XivOperand>(fun (c, l, r) ->
                let l = l :> IOperand<XivOperand>
                match c with
                | '+' -> l.Add(r)
                | '-' -> l.Sub(r)
                | '*' -> l.Mul(r)
                | '/' -> l.Div(r)
                | _ ->  failwithf ""
            )
            x.EvalWith(str, func)

        member x.TryEval(str : string) = 
            try
                let ret = x.Eval(str)
                Ok (ret)
            with
            |e -> 
                Error e

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
        sw.WriteLine("名称 平均 低 中 高 更新时间")
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
                    let avg  = o |> Array.averageBy (fun item -> item.Price |> float)
                    let upd  = 
                        let max = o |> Array.maxBy (fun item -> item.TimeStamp)
                        max.GetHumanReadableTimeSpan()
                    sw.WriteLine("{0} {1} {2:n0} {3:n0} {4:n0} {5}", i.Name, stdev, low, avg, high, upd)
                | Error err ->
                    sw.WriteLine("{0} 服务器处理失败，请稍后重试", i.Name)
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("market", "查询市场订单", "物品Id或全名...")>]
    member x.HandleMarket(msgArg : CommandArgs) = 
        let sw = new IO.StringWriter()
        let (succ, world, args) = CommandUtils.GetWorldWithDefault(msgArg.Arguments)
        if succ then
            sw.WriteLine("服务器：{0}", world.WorldName)
        else
            sw.WriteLine("默认服务器：{0}", world.WorldName)
        sw.WriteLine("名称 价格(前25%订单) 低 更新时间")
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
                    sw.WriteLine("{0} {1} {2:n0} {3}", i.Name, stdev, low, upd)
                | Error err ->
                    sw.WriteLine("{0} 服务器处理失败，请稍后重试", i.Name)
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())
    
    [<CommandHandlerMethodAttribute("alltradelog", "查询全服交易记录", "物品Id或全名...")>]
    member x.HandleTradelogCrossWorld(msgArg : CommandArgs) = 
        let sw = new IO.StringWriter()
        sw.WriteLine("名称 土豆 平均 低 中 高 最后成交")
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
                        let avg  = o |> Array.averageBy (fun item -> item.Price |> float)
                        let upd  = 
                            let max = o |> Array.maxBy (fun item -> item.TimeStamp)
                            max.GetHumanReadableTimeSpan()
                        sw.WriteLine("{0} {1} {2} {3:n0} {4:n0} {5:n0} {6}", i.Name, world.WorldName,  stdev, low, avg, high, upd)
                | Error err ->
                    sw.WriteLine("{0} 服务器处理失败，请稍后重试", i.Name)
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("allmarket", "查询全服市场订单", "物品Id或全名...")>]
    member x.HandleMarketCrossWorld(msgArg : CommandArgs) =  
        let sw = new IO.StringWriter()
        sw.WriteLine("名称 土豆 价格(前25%订单) 低 更新时间")
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
                        sw.WriteLine("{0} {1} {2} {3:n0} {4}", i.Name, server.WorldName,  stdev, low, upd)
                | Error err ->
                    sw.WriteLine("{0} 服务器处理失败，请稍后重试", i.Name)
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("is", "查找名字包含字符的物品", "关键词...")>]
    member x.HandleItemSearch(msgArg : CommandArgs) = 
        let sw = new IO.StringWriter()
        sw.WriteLine("查询 物品 Id")
        for i in msgArg.Arguments do 
            x.Logger.Trace("查询{0}", i)
            let ret = itemCol.SearchByName(i)
            if ret.Length = 0 then
                sw.WriteLine("{0} 无 无 无", i)
            else
                for item in ret do 
                    sw.WriteLine("{0} {1} {2}", i, item.Name, item.Id)
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("recipesumold", "（备用）查找多个物品的材料，不查询价格", "物品Id或全名...")>]
    member x.HandleRecipeSumOld(msgArg : CommandArgs) = 
        let sw = new IO.StringWriter()
        let sumer = new Recipe.FinalMaterials()

        for i in msgArg.Arguments do 
            match strToItem(i) with
            | None -> sw.WriteLine("找不到物品{0}，请尝试#is {0}", i)
            | Some(i) ->
                let recipe = rm.GetMaterialsOne(i)
                if recipe.Length = 0 then
                    sw.WriteLine("{0} 没有生产配方", i.Name)
                else
                    recipe
                    |> Array.iter (fun (item, count) -> sumer.AddOrUpdate(item, count |> float))
        sw.WriteLine("物品 数量")
        for (i, c) in sumer.Get() |> Array.sortBy (fun (item,_) -> item.Id) do
            sw.WriteLine("{0} {1}", i.Name, c)
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("recipesum", "根据表达式汇总多个物品的材料，不查询价格\r\n大于50数字视为物品ID", "物品Id或全名...")>]
    member x.HandleRecipeSumExpr(msgArg : CommandArgs) = 
        let sw = new IO.StringWriter()
        let acc = new XivExpression.Accumulator()
        let parser = new XivExpression.XivExpression()
        for str in msgArg.Arguments do 
            match parser.TryEval(str) with
            | Error err ->
                sw.WriteLine("对{0}求值时发生错误\r\n{1}", str, err)
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
        sw.WriteLine("物品 数量")
        let final =
            acc
            |> Seq.toArray
            |> Array.map (fun x -> (x.Key, (ceil x.Value) |> int))
            |> Array.sortBy (fun (i, r) -> i.Id)
        for (item, amount) in final do 
            sw.WriteLine("{0} {1}", item.Name, amount)
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("recipefinal", "查找物品最终材料", "物品Id或全名...")>]
    member x.HandleItemFinalRecipe(msgArg : CommandArgs) = 
        let sw = new IO.StringWriter()
        let (succ, world, args) = CommandUtils.GetWorldWithDefault(msgArg.Arguments)
        if succ then
            sw.WriteLine("服务器：{0}", world.WorldName)
        else
            sw.WriteLine("默认服务器：{0}", world.WorldName)
        sw.WriteLine("查询 物品 价格(前25%订单) 需求 总价 更新时间")
        for i in args do 
            match strToItem(i) with
            | None -> sw.WriteLine("找不到物品{0}，请尝试#is {0}", i)
            | Some(i) ->
                let recipe = rm.GetMaterialsRec(i)
                let mutable sum = MarketUtils.StdEv.Zero
                for (item, count) in recipe do 
                    let ret = 
                        let itemId = item.Id |> uint32
                        async {
                            let worldId = world.WorldId
                            return! MarketOrder.MarketOrderProxy.call <@ fun server -> server.GetByIdWorld worldId itemId @>
                        } |> Async.RunSynchronously
                    let price = MarketUtils.GetStdEvMarket(ret.Orders, cutoff)
                    let total = price * count
                    sum <- sum + total
                    sw.WriteLine("{0} {1} {2:n0} {3:n0} {4:n0} {5}",
                        i, item.Name, price, count, total, ret.GetHumanReadableTimeSpan() )
                sw.WriteLine("{0} 总计 {1} -- -- --", i, sum)
                sw.WriteLine()
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("mentor", "今日导随运势", "")>]
    member x.HandleMentor(msgArg : CommandArgs)= 
        (*
        let sw = new IO.StringWriter()
        let dicer = new Utils.Dicer(Utils.SeedOption.SeedByUserDay(msgArg.MessageEvent))

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
            let a = dicer.GetRandomItems(MentorUtils.shouldOrAvoid, 3 * 2)
            a.[0..2], a.[3..]
        sw.WriteLine("宜：{0}", String.concat "/" s)
        sw.WriteLine("忌：{0}", String.concat "/" a)
        let c, jobs = dicer.GetRandomItem(MentorUtils.classJob)
        let job = dicer.GetRandomItem(jobs)
        sw.WriteLine("推荐职业: {0} {1}", c, job)
        sw.WriteLine("推荐排本场所: {0}", dicer.GetRandomItem(MentorUtils.location).ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())
        *)
        msgArg.CqEventArgs.QuickMessageReply("功能维护")