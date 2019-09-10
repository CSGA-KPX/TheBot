module XivModule
open System
open KPX.TheBot.WebSocket
open KPX.TheBot.WebSocket.Instance
open LibFFXIV.ClientData
open LibXIVServer

module MarketUtils = 
    let internal TakeMarketSample (samples : LibXIVServer.MarketV2.ServerMarkerOrder [] , cutPct : int) = 
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

    let GetStdEvMarket(market : LibXIVServer.MarketV2.ServerMarkerOrder [] , cutPct : int) = 
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

    let GetStdEvTrade(tradelog : LibXIVServer.TradeLogV2.ServerTradeLog []) = 
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

module MentorUtils = 
    let fortune = 
        [|
            "大吉", "行会令连送/三导师高铁四人本/假风火土白给".Split('/')
            "小吉", "豆芽已看攻略/稳定7拖1".Split('/')
            "平"  , "听话懂事初见/不急不缓四人本/超越之力25%".Split('/')
            "小凶", "塔三团灭/遇假火/260T白山堡".Split('/')
            "大凶", "嘴臭椰新/装会假火/极神小龙虾".Split('/')
        |]
    let shouldOrAvoid = "行会令，水晶塔，魔航船，马哈，影之国，欧米伽时空狭缝，亚历山大机工城，幻龙残骸秘约之塔，魔科学研究所，极水神，极雷神，极冰神，极莫古力，极三斗神，神龙歼灭战，动画城，血亏堡，中途参战，红色划水，蓝色carry，绿色擦屁股，辱骂毒豆芽，辱骂假火，副职导随".Split('，')
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

type XivModule() = 
    inherit HandlerModuleBase()
    let tradelog = new TradeLogV2.TradeLogDAO()
    let market   = new MarketV2.MarketOrderDAO()
    let rm       = Recipe.RecipeManager.GetInstance()
    let cutoff   = 25
    let itemNames= Item.AllItems.Value |> Array.map (fun x -> (x.Name.ToLowerInvariant(), x))
    let isNumber(str : string) = 
        if str.Length <> 0 then
            String.forall (Char.IsDigit) str
        else
            false
    let strToItem(str : string)= 
        if isNumber(str) then
            Item.LookupById(Convert.ToInt32(str))
        else
            Item.LookupByName(str)

    member private x.HandleTradelog(query : string) = 
        let sw = new IO.StringWriter()
        sw.WriteLine("服务器：拉诺西亚")
        sw.WriteLine("名称 平均 低 中 高 更新时间")
        for i in query.Split(' ').[1..] do 
            match strToItem(i) with
            | None -> sw.WriteLine("找不到物品{0}，请尝试#is {0}", i)
            | Some(i) ->
                let ret = tradelog.Get(i.Id |> uint32)
                match ret with
                | _ when ret.Record.IsNone ->
                    sw.WriteLine("{0} 服务器处理失败，请稍后重试", i.Name)
                | _ when ret.Record.Value.Length = 0 ->
                    sw.WriteLine("{0} 无记录", i.Name)
                | _ ->
                    let o = ret.Record.Value
                    let stdev= MarketUtils.GetStdEvTrade(o)
                    let low  = o |> Array.map (fun item -> item.Price) |> Array.min
                    let high = o |> Array.map (fun item -> item.Price) |> Array.max
                    let avg  = o |> Array.averageBy (fun item -> item.Price |> float)
                    let upd  = ret.UpdateDate
                    sw.WriteLine("{0} {1} {2:n} {3:n} {4:n} {5}", i.Name, stdev, low, avg, high, upd)
        sw.ToString()

    member private x.HandleMarket(query : string) = 
        let sw = new IO.StringWriter()
        sw.WriteLine("服务器：拉诺西亚")
        sw.WriteLine("名称 价格(前25%订单) 低 更新时间")
        for i in query.Split(' ').[1..] do 
            match strToItem(i) with
            | None -> sw.WriteLine("找不到物品{0}，请尝试#is {0}", i)
            | Some(i) ->
                let ret = market.Get(i.Id |> uint32)
                match ret with
                | _ when ret.Record.IsNone ->
                    sw.WriteLine("{0} 服务器处理失败，请稍后重试", i.Name)
                | _ when ret.Record.Value.Length = 0 ->
                    sw.WriteLine("{0} 无记录", i.Name)
                | _ ->
                    let o    = ret.Record.Value
                    let stdev= MarketUtils.GetStdEvMarket(o, 25)
                    let low  = o |> Array.map (fun item -> item.Price) |> Array.min
                    let upd  = ret.UpdateDate
                    sw.WriteLine("{0} {1} {2:n} {3}", i.Name, stdev, low, upd)
        sw.ToString()

    member private x.HandleItemSearch (query : string) = 
        let sw = new IO.StringWriter()
        sw.WriteLine("查询 物品 Id")
        for i in query.Split(' ').[1..] do 
            let q = i.ToLowerInvariant()
            x.Logger.Trace("查询{0}", q)
            let ret = itemNames |> Array.filter (fun (n, _) -> n.Contains(q))
            if ret.Length = 0 then
                sw.WriteLine("{0} 无 无 无", i)
            else
                for r in ret do 
                    let item= (snd r)
                    sw.WriteLine("{0} {1} {2}", i, item.Name, item.Id)
        sw.ToString()

    member private x.HandleItemFinalRecipe (query : string) = 
        let sw = new IO.StringWriter()
        sw.WriteLine("服务器：拉诺西亚")
        sw.WriteLine("查询 物品 价格(前25%订单) 需求 总价 更新时间")
        for i in query.Split(' ').[1..] do 
            match strToItem(i) with
            | None -> sw.WriteLine("找不到物品{0}，请尝试#is {0}", i)
            | Some(i) ->
                let recipe = rm.GetMaterialsRec(i)
                let mutable sum = MarketUtils.StdEv.Zero
                for (item, count) in recipe do 
                    let ret = market.Get(item.Id |> uint32)
                    let price = MarketUtils.GetStdEvMarket(ret.Record.Value, cutoff)
                    let total = price * count
                    sum <- sum + total
                    sw.WriteLine("{0} {1} {2} {3} {4} {5}",
                        i, item.Name, price, count, total, ret.UpdateDate )
                sw.WriteLine("{0} 总计 {1} -- -- --", i, sum)
                sw.WriteLine()
        sw.ToString()

    member private x.HandleMentor(msg : DataType.Event.Message.MessageEvent) = 
        let sw = new IO.StringWriter()
        let dicer = new Utils.Dicer(Utils.SeedOption.SeedByUserDay, msg)
        let s,a   = 
            let a = dicer.GetRandomItems(MentorUtils.shouldOrAvoid, 3 * 2)
            a.[0..2], a.[3..]
        
        let fortune, events = 
            match dicer.GetRandom(100u) with
            | x when x <= 5  -> MentorUtils.fortune.[0]
            | x when x <= 20 -> MentorUtils.fortune.[1]
            | x when x <= 80 -> MentorUtils.fortune.[2]
            | x when x <= 95 -> MentorUtils.fortune.[3]
            | _              -> MentorUtils.fortune.[4]
        let event = dicer.GetRandomItem(events)

        sw.WriteLine("{0} 今日导随运势为：", x.ToNicknameOrCard(msg))
        sw.WriteLine("{0} : {1}", fortune, event)
        sw.WriteLine("宜：{0}", String.concat "/" s)
        sw.WriteLine("忌：{0}", String.concat "/" a)
        let c, jobs = dicer.GetRandomItem(MentorUtils.classJob)
        let job = dicer.GetRandomItem(jobs)
        sw.WriteLine("推荐职业: {0} {1}", c, job)
        sw.WriteLine("推荐排本场所: {0}", dicer.GetRandomItem(MentorUtils.location).ToString())
        sw.ToString()

    override x.MessageHandler _ arg =
        let str = arg.Data.Message.ToString()
        match str.ToLowerInvariant() with
        | s when s.StartsWith("#tradelog") -> 
            x.QuickMessageReply(arg, x.HandleTradelog(s))
        | s when s.StartsWith("#market") -> 
            x.QuickMessageReply(arg, x.HandleMarket(s))
        | s when s.StartsWith("#is") -> 
            x.QuickMessageReply(arg, x.HandleItemSearch(s))
        | s when s.StartsWith("#recipefinal") -> 
            x.QuickMessageReply(arg, x.HandleItemFinalRecipe(s))
        | s when s.StartsWith("#mentor") -> 
            x.QuickMessageReply(arg, x.HandleMentor(arg.Data))
        | _ -> ()

