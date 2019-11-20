module TheBot.Module.XivModule.Utils
open System
open LibDmfXiv
open XivData

module MarketUtils =
    open LibDmfXiv.Client

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

        static member (/) (x : StdEv, y : float) = 
            {
                Average   = x.Average / y
                Deviation = x.Deviation / y
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
            if data.Length = 0 then
                { Average = nan; Deviation = nan }
            else
                let avg = Array.average data
                let sum = data |> Array.sumBy (fun x -> (x - avg) ** 2.0)
                let ev  = sum / (float data.Length)
                { Average = avg; Deviation = sqrt ev }

    type MarketData = 
        | Order of Shared.MarketOrder.FableMarketOrder
        | Trade of Shared.TradeLog.FableTradeLog

        member x.ItemRecord = 
            match x with
            | Order x -> Item.ItemCollection.Instance.LookupById(x.ItemId |> int)
            | Trade x -> Item.ItemCollection.Instance.LookupById(x.ItemId |> int)

        member x.IsHq = 
            match x with
            | Order x -> x.IsHQ
            | Trade x -> x.IsHQ

        member x.Count =
            match x with
            | Order x -> x.Count
            | Trade x -> x.Count

        member x.Price =
            match x with
            | Order x -> x.Price
            | Trade x -> x.Price
        
        /// 返回GMT+8的时间
        member x.UpdateTime = 
            let ts = 
                match x with
                | Order x -> x.TimeStamp
                | Trade x -> x.TimeStamp
            DateTimeOffset.FromUnixTimeSeconds(ts |> int64).ToOffset(TimeSpan.FromHours(8.0))

    type MarketAnalyzer(item : Item.ItemRecord, data : MarketData[]) = 
        member x.ItemRecord = item
        member x.IsEmpty = data.Length = 0
        
        member x.LastUpdateTime() = 
            let dt = (data |> Array.maxBy (fun x -> x.UpdateTime)).UpdateTime
            (DateTimeOffset.Now - dt) |> Shared.Utils.formatTimeSpan

        member x.MinPrice() = (data |> Array.minBy (fun x -> x.Price)).Price
        member x.MaxPrice() = (data |> Array.maxBy (fun x -> x.Price)).Price
        member x.StdEvPrice() = data |> Array.map (fun x -> x.Price |> float) |> StdEv.FromData

        member x.MinCount() = (data |> Array.minBy (fun x -> x.Count)).Count
        member x.MaxCount() = (data |> Array.maxBy (fun x -> x.Count)).Count
        member x.StdEvCount() = data |> Array.map (fun x -> x.Count |> float) |> StdEv.FromData

        member x.TakeNQ() = new MarketAnalyzer(item, data |> Array.filter (fun x -> not x.IsHq))
        member x.TakeHQ() = new MarketAnalyzer(item, data |> Array.filter (fun x -> x.IsHq))

        member x.TakeVolume(cutPct : int) = 
            new MarketAnalyzer(item, [|
                let samples = data |> Array.sortBy (fun x -> x.Price)
                let itemCount = data |> Array.sumBy (fun x -> x.Count |> int)
                let cutLen = itemCount * cutPct / 100
                let mutable rest = cutLen
                match itemCount = 0 , cutLen = 0 with
                | true, _ -> ()
                | false, true ->
                    //返回第一个
                    yield data.[0]
                | false, false ->
                    for record in samples do
                        let takeCount = min rest (record.Count |> int)
                        if takeCount <> 0 then
                            rest <- rest - takeCount
                            yield record
            |])

        static member FetchOrdersWorld(item : Item.ItemRecord, world : World.World) = 
            let itemId = item.Id |> uint32
            let worldId = world.WorldId
            MarketOrder.MarketOrderProxy.callSafely <@ fun server -> server.GetByIdWorld worldId itemId @>
            |> Async.RunSynchronously
            |> Result.map (fun o -> o.Orders |> Array.map (Order))
            |> Result.map (fun o -> new MarketAnalyzer(item, o))

        static member FetchOrdersAllWorld(item : Item.ItemRecord) = 
            let itemId = item.Id |> uint32
            MarketOrder.MarketOrderProxy.callSafely <@ fun server -> server.GetByIdAllWorld itemId @>
            |> Async.RunSynchronously
            |> Result.map (fun oa -> 
                [|  for o in oa do 
                        let world = World.WorldFromId.[o.WorldId]
                        let ma    = new MarketAnalyzer(item, o.Orders |> Array.map (Order))
                        yield world, ma|])

        static member FetchTradesWorld(item : Item.ItemRecord, world : World.World) = 
            let itemId = item.Id |> uint32
            let worldId = world.WorldId
            TradeLog.TradelogProxy.callSafely <@ fun server -> server.GetByIdWorld worldId itemId 20 @>
            |> Async.RunSynchronously
            |> Result.map (Array.map (Trade))
            |> Result.map (fun o -> new MarketAnalyzer(item, o))

        static member FetchTradesAllWorld(item : Item.ItemRecord) =
            let itemId = item.Id |> uint32
            TradeLog.TradelogProxy.callSafely <@ fun server -> server.GetByIdAllWorld itemId 20 @>
            |> Async.RunSynchronously
            |> Result.map (fun t -> 
                t
                |> Array.groupBy (fun t -> World.WorldFromId.[t.WorldId])
                |> Array.map (fun (x,y) -> (x, new MarketAnalyzer(item, y |> Array.map (Trade)))))

module MentorUtils = 
    open XivData.Mentor
    let fortune = 
        [|
            "大吉", "行会令连送/三导师高铁四人本/假风火土白给".Split('/')
            "小吉", "豆芽已看攻略/稳定7拖1".Split('/')
            "平"  , "听话懂事初见/不急不缓四人本/超越之力25%".Split('/')
            "小凶", "塔三团灭/遇假火/260T白山堡".Split('/')
            "大凶", "嘴臭椰新/装会假火/极神小龙虾".Split('/')
        |]

    let shouldOrAvoid = ShouldOrAvoidCollection.Instance

    let classJob = 
        [|
            "红", "近战，远敏，复活机，法系".Split('，')
            "绿", "崩石怪，小仙女，游戏王".Split('，')
            "蓝", "裂石飞环，神圣领域，暗技人".Split('，')
        |]

    let location = LocationCollection.Instance

module CommandUtils =
    let formatNumber (i : uint32) = System.String.Format("{0:N0}", i)

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
    open TheBot.GenericRPN

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

    type XivExpression() as x = 
        inherit GenericRPNParser<XivOperand>()

        let tokenRegex = new Regex("([\-+*/()#])", RegexOptions.Compiled)

        do 
            //Uniary operator
            let itemOperator = new GenericOperator('#', Int32.MaxValue, IsBinary = false)
            x.AddOperator(itemOperator)
        override x.Tokenize(str) = 
            [|
                let strs = tokenRegex.Split(str) |> Array.filter (fun x -> x <> "")
                for str in strs do
                    match str with
                    | _ when String.forall Char.IsDigit str ->
                        let num = str |> int
                        yield Operand (Number (num |> float))
                    | _ when x.Operatos.ContainsKey(str) -> 
                        yield Operator (x.Operatos.[str])
                    | _ -> 
                        let item = Item.ItemCollection.Instance.TryLookupByName(str)
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
                | '#' ->
                    // l = r when uniary
                    match r with
                    | Number f -> 
                        let item = Item.ItemCollection.Instance.LookupById(int f)
                        let acu  = Accumulator.Singleton item
                        Accumulator acu
                    | Accumulator a -> failwithf "#符号仅对数字使用"
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
