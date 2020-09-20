module TheBot.Module.XivModule.Utils

open System
open LibDmfXiv

open BotData.XivData

module MarketUtils =
    open LibDmfXiv.Client

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
        | Order of Shared.MarketOrder.FableMarketOrder
        | Trade of Shared.TradeLog.FableTradeLog

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
            (DateTimeOffset.Now - dt) |> Shared.Utils.formatTimeSpan

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

        static member FetchOrdersWorld(item : Item.ItemRecord, world : World.World) =
            let itemId = item.Id |> uint32
            let worldId = world.WorldId
            MarketOrder.MarketOrderProxy.callSafely <@ fun server -> server.GetByIdWorld worldId itemId @>
            |> Async.RunSynchronously
            |> Result.map (fun o -> o.Orders |> Array.map (Order))
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

        static member FetchTradesWorld(item : Item.ItemRecord, world : World.World) =
            let itemId = item.Id |> uint32
            let worldId = world.WorldId
            TradeLog.TradelogProxy.callSafely <@ fun server -> server.GetByIdWorld worldId itemId 20 @>
            |> Async.RunSynchronously
            |> Result.map (Array.map (Trade))
            |> Result.map (fun o -> MarketAnalyzer(item, world, o))

        static member FetchTradesAllWorld(item : Item.ItemRecord) =
            let itemId = item.Id |> uint32
            TradeLog.TradelogProxy.callSafely <@ fun server -> server.GetByIdAllWorld itemId 20 @>
            |> Async.RunSynchronously
            |> Result.map (fun t ->
                t
                |> Array.groupBy (fun t -> World.WorldFromId.[t.WorldId])
                |> Array.map (fun (x, y) -> MarketAnalyzer(item, x, y |> Array.map (Trade))))

module MentorUtils =
    open BotData.XivData.Mentor

    let fortune =
        [| "大吉", "行会令连送/三导师高铁四人本/假风火土白给".Split('/')
           "小吉", "豆芽已看攻略/稳定7拖1".Split('/')
           "平", "听话懂事初见/不急不缓四人本/超越之力25%".Split('/')
           "小凶", "塔三团灭/遇假火/260T白山堡".Split('/')
           "大凶", "嘴臭椰新/装会假火/极神小龙虾".Split('/') |]

    let shouldOrAvoid = ShouldOrAvoidCollection.Instance

    let classJob =
        [| "红", "近战，远敏，复活机，法系".Split('，')
           "绿", "崩石怪，小仙女，游戏王".Split('，')
           "蓝", "裂石飞环，神圣领域，暗技人".Split('，') |]

    let location = LocationCollection.Instance

module CommandUtils =
    open TheBot.Utils.Config
    open KPX.FsCqHttp.Utils.UserOption

    let defaultServerKey = "defaultServerKey"

    type XivConfig (args : KPX.FsCqHttp.Handler.CommandHandlerBase.CommandArgs) = 
        let opts = UserOptionParser()
        let cm = ConfigManager(ConfigOwner.User (args.MessageEvent.UserId))

        let defaultServerName = "拉诺西亚"
        let defaultServer = World.WorldFromName.[defaultServerName]

        do
            opts.RegisterOption("text", "")
            opts.RegisterOption("server", defaultServerName)

            args.Arguments
            |> Array.map (fun str -> 
                if World.WorldFromName.ContainsKey(str) then
                    "server:"+str
                else
                    str)
            |> opts.Parse

        member x.IsWorldDefined = opts.IsDefined("server")

        /// 获得查询目标服务器
        ///
        /// 用户指定 -> 用户配置 -> 默认（拉诺西亚）
        member x.GetWorld() = 
            if x.IsWorldDefined then
                World.WorldFromName.[opts.GetValue("server")]
            else
                cm.Get(defaultServerKey, defaultServer)

        member x.CommandLine = opts.CommandLine

        member x.IsImageOutput = not <| opts.IsDefined("text")

    let XivSpecialChars = 
        [|
            '\ue03c' // HQ
            '\ue03d' //收藏品
        |]

module XivExpression =
    open TheBot.Utils.GenericRPN
    open TheBot.Utils.RecipeRPN

    type ItemAccumulator = ItemAccumulator<Item.ItemRecord>

    type XivExpression() as x = 
        inherit RecipeExpression<Item.ItemRecord>()
        
        do
            let itemOperator = GenericOperator<_>('#', Int32.MaxValue, fun l r ->
                match l with
                | Number f ->
                    let item = Item.ItemCollection.Instance.GetByItemId(int f)
                    let acu = ItemAccumulator.Singleton item
                    Accumulator acu
                | Accumulator a -> failwithf "#符号仅对数字使用")

            itemOperator.IsBinary <- false
            x.Operatos.Add(itemOperator)

        override x.TryGetItemByName(str) = 
            Item.ItemCollection.Instance.TryGetByName(str.TrimEnd(CommandUtils.XivSpecialChars))