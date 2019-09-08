module XivModule
open System
open KPX.TheBot.WebSocket
open KPX.TheBot.WebSocket.Instance
open LibFFXIV.ClientData
open LibXIVServer

module Utils = 
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

type XivModule() = 
    inherit HandlerModuleBase()
    let tradelog = new TradeLogV2.TradeLogDAO()
    let market   = new MarketV2.MarketOrderDAO()
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
        sw.WriteLine("名称 低 中 高 更新时间")
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
                    let stdev= Utils.GetStdEvTrade(o)
                    let low  = o |> Array.map (fun item -> item.Price) |> Array.min
                    let high = o |> Array.map (fun item -> item.Price) |> Array.max
                    let avg  = o |> Array.averageBy (fun item -> item.Price |> float)
                    let upd  = ret.UpdateDate
                    sw.WriteLine("{0} {1} {2:n} {3:n} {4:n} {5}", i.Name, stdev, low, avg, high, upd)
        sw.ToString()

    member private x.HandleMarket(query : string) = 
        let sw = new IO.StringWriter()
        sw.WriteLine("服务器：拉诺西亚")
        sw.WriteLine("名称 价格(前25%订单) 低 中 高 更新时间")
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
                    let o = ret.Record.Value
                    let stdev= Utils.GetStdEvMarket(o, 25)
                    let low  = o |> Array.map (fun item -> item.Price) |> Array.min
                    let high = o |> Array.map (fun item -> item.Price) |> Array.max
                    let avg  = o |> Array.averageBy (fun item -> item.Price |> float)
                    let upd  = ret.UpdateDate
                    sw.WriteLine("{0} {1} {2:n} {3:n} {4:n} {5}", i.Name, stdev, low, avg, high, upd)
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

    override x.MessageHandler _ arg =
        let str = arg.Data.Message.ToString()
        match str.ToLowerInvariant() with
        | s when s.StartsWith("#tradelog") -> 
            x.QuickMessageReply(arg, x.HandleTradelog(s))
        | s when s.StartsWith("#market") -> 
            x.QuickMessageReply(arg, x.HandleMarket(s))
        | s when s.StartsWith("#is") -> 
            x.QuickMessageReply(arg, x.HandleItemSearch(s))
        | _ -> ()

