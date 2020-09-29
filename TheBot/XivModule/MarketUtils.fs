module TheBot.Module.XivModule.Utils.MarketUtils

open System
open LibDmfXiv.Shared

open BotData.XivData

type StdEv =
    { Average : float
      Deviation : float }

    member x.Ceil() = 
        { Average = x.Average |> ceil
          Deviation = x.Deviation |> ceil}

    member x.Floor() =
        { Average = x.Average |> floor
          Deviation = x.Deviation |> floor}

    member x.Round() =
        { Average = x.Average |> round
          Deviation = x.Deviation |> round}

    override x.ToString() = String.Format("{0:n0}±{1:n0}", x.Average, x.Deviation)

    static member (*) (x : StdEv, y : float) =
        { Average = x.Average * y
          Deviation = x.Deviation * y }

    static member (/) (x : StdEv, y : float) =
        { Average = x.Average / y
          Deviation = x.Deviation / y }

    static member (+) (x : StdEv, y : StdEv) =
        { Average = x.Average + y.Average
          Deviation = x.Deviation + y.Deviation }

    static member (-) (x : StdEv, y : StdEv) =
        { Average = x.Average - y.Average
          Deviation = x.Deviation - y.Deviation }

    static member Zero =
        { Average = 0.0
          Deviation = 0.0 }

    static member FromData(data : float []) =
        if data.Length = 0 then
            { Average = nan
              Deviation = nan }
        else
            let avg = Array.average data
            let sum = data |> Array.sumBy (fun x -> (x - avg) ** 2.0)
            let ev = sum / (float data.Length)
            { Average = avg
              Deviation = sqrt ev }

type MarketData =
    | Order of MarketOrder.FableMarketOrder
    | Trade of TradeLog.FableTradeLog

    member x.ItemRecord =
        match x with
        | Order x -> Item.ItemCollection.Instance.GetByItemId(x.ItemId |> int)
        | Trade x -> Item.ItemCollection.Instance.GetByItemId(x.ItemId |> int)

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

type MarketAnalyzer(item : Item.ItemRecord, world : World.World, data : MarketData []) =

    member x.World = world
    member x.ItemRecord = item
    member x.IsEmpty = data.Length = 0
    member x.Data = data

    member x.LastUpdateTime() =
        let dt = (data |> Array.maxBy (fun x -> x.UpdateTime)).UpdateTime
        (DateTimeOffset.Now - dt) |> Utils.formatTimeSpan

    member x.MinPrice() = (data |> Array.minBy (fun x -> x.Price)).Price
    member x.MaxPrice() = (data |> Array.maxBy (fun x -> x.Price)).Price

    member x.StdEvPrice() =
        data
        |> Array.map (fun x -> x.Price |> float)
        |> StdEv.FromData

    member x.MinCount() = (data |> Array.minBy (fun x -> x.Count)).Count
    member x.MaxCount() = (data |> Array.maxBy (fun x -> x.Count)).Count

    member x.StdEvCount() =
        data
        |> Array.map (fun x -> x.Count |> float)
        |> StdEv.FromData

    member x.TakeNQ() = MarketAnalyzer(item, world, data |> Array.filter (fun x -> not x.IsHq))
    member x.TakeHQ() = MarketAnalyzer(item, world, data |> Array.filter (fun x -> x.IsHq))

    /// 默认25%市场容量
    member x.TakeVolume() = x.TakeVolume(25)

    member x.TakeVolume(cutPct : int) =
        MarketAnalyzer(item, world,
                           [| let samples = data |> Array.sortBy (fun x -> x.Price)
                              let itemCount = data |> Array.sumBy (fun x -> x.Count |> int)
                              let cutLen = itemCount * cutPct / 100
                              let mutable rest = cutLen
                              match itemCount = 0, cutLen = 0 with
                              | true, _ -> ()
                              | false, true ->
                                  //返回第一个
                                  yield data.[0]
                              | false, false ->
                                  for record in samples do
                                      let takeCount = min rest (record.Count |> int)
                                      if takeCount <> 0 then
                                          rest <- rest - takeCount
                                          yield record |])

(*
    static member FetchOrdersWorld(item : Item.ItemRecord, world : World.World) =

        let itemId = item.Id |> uint32
        let worldId = world.WorldId
        MarketOrder.MarketOrderProxy.callSafely <@ fun server -> server.GetByIdWorld worldId itemId @>
        |> Async.RunSynchronously
        |> Result.map (fun o -> o.Orders |> Array.map (Order))
        |> Result.map (fun o -> MarketAnalyzer(item, world, o))

    static member FetchTradesWorld(item : Item.ItemRecord, world : World.World) =
        let itemId = item.Id |> uint32
        let worldId = world.WorldId
        TradeLog.TradelogProxy.callSafely <@ fun server -> server.GetByIdWorld worldId itemId 20 @>
        |> Async.RunSynchronously
        |> Result.map (Array.map (Trade))
        |> Result.map (fun o -> MarketAnalyzer(item, world, o))


    static member FetchOrdersAllWorld(item : Item.ItemRecord) =
        let itemId = item.Id |> uint32
        MarketOrder.MarketOrderProxy.callSafely <@ fun server -> server.GetByIdAllWorld itemId @>
        |> Async.RunSynchronously
        |> Result.map (fun oa ->
            [| for o in oa do
                let world = World.WorldFromId.[o.WorldId]
                let ma = MarketAnalyzer(item, world, o.Orders |> Array.map (Order))
                yield ma |])

    static member FetchTradesAllWorld(item : Item.ItemRecord) =
        let itemId = item.Id |> uint32
        TradeLog.TradelogProxy.callSafely <@ fun server -> server.GetByIdAllWorld itemId 20 @>
        |> Async.RunSynchronously
        |> Result.map (fun t ->
            t
            |> Array.groupBy (fun t -> World.WorldFromId.[t.WorldId])
            |> Array.map (fun (x, y) -> MarketAnalyzer(item, x, y |> Array.map (Trade))))
*)