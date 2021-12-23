module KPX.XivPlugin.Modules.Utils.MarketUtils

open System

open KPX.FsCqHttp.Utils.AliasMapper

open KPX.XivPlugin.Data


type StdEv =
    { Average: float
      Deviation: float }

    member x.Ceil() =
        { Average = x.Average |> ceil
          Deviation = x.Deviation |> ceil }

    member x.Floor() =
        { Average = x.Average |> floor
          Deviation = x.Deviation |> floor }

    member x.Round() =
        { Average = x.Average |> round
          Deviation = x.Deviation |> round }

    override x.ToString() =
        String.Format("{0:n0}±{1:n0}", x.Average, x.Deviation)

    static member (*)(x: StdEv, y: float) =
        { Average = x.Average * y
          Deviation = x.Deviation * y }

    static member (/)(x: StdEv, y: float) =
        { Average = x.Average / y
          Deviation = x.Deviation / y }

    static member (+)(x: StdEv, y: StdEv) =
        { Average = x.Average + y.Average
          Deviation = x.Deviation + y.Deviation }

    static member (-)(x: StdEv, y: StdEv) =
        { Average = x.Average - y.Average
          Deviation = x.Deviation - y.Deviation }

    static member Zero = { Average = 0.0; Deviation = 0.0 }

    static member FromData(data: float []) =
        if data.Length = 0 then
            { Average = nan; Deviation = nan }
        else
            let avg = Array.average data

            let sum = data |> Array.sumBy (fun x -> (x - avg) ** 2.0)

            let ev = sum / (float data.Length)
            { Average = avg; Deviation = sqrt ev }

[<Struct>]
type MarketData =
    | Order of odrItem: MarketOrder
    | Trade of logItem: TradeLog

    member x.IsHq =
        match x with
        | Order x -> x.IsHQ
        | Trade x -> x.IsHQ

    member x.Quantity =
        match x with
        | Order x -> x.Quantity
        | Trade x -> x.Quantity

    member x.Price =
        match x with
        | Order x -> x.PricePerUnit
        | Trade x -> x.PricePerUnit

    /// 返回GMT+8的时间
    member x.UpdateTime =
        match x with
        | Order x -> x.LastReviewTime
        | Trade x -> x.TimeStamp

type MarketAnalyzer(item: XivItem, world: World, data: MarketData []) =

    member x.World = world
    member x.ItemRecord = item
    member x.IsEmpty = data.Length = 0
    member x.Data = data

    member x.LastUpdateTime() =
        if data.Length = 0 then
            TimeSpan.MaxValue
        else
            let data = (data |> Array.maxBy (fun x -> x.UpdateTime))

            let dt = DateTimeOffset.FromUnixTimeSeconds(data.UpdateTime)

            (DateTimeOffset.Now - dt)

    member x.MinPrice() =
        if data.Length = 0 then
            nan
        else
            (data |> Array.minBy (fun x -> x.Price)).Price |> float

    member x.MaxPrice() =
        if data.Length = 0 then
            nan
        else
            (data |> Array.maxBy (fun x -> x.Price)).Price |> float

    member x.StdEvPrice() =
        data |> Array.map (fun x -> x.Price |> float) |> StdEv.FromData

    member x.MinCount() =
        if data.Length = 0 then
            0
        else
            (data |> Array.minBy (fun x -> x.Quantity))
                .Quantity

    member x.MaxCount() =
        if data.Length = 0 then
            0
        else
            (data |> Array.maxBy (fun x -> x.Quantity))
                .Quantity

    member x.StdEvCount() =
        data |> Array.map (fun x -> x.Quantity |> float) |> StdEv.FromData

    member x.TakeNQ() =
        MarketAnalyzer(item, world, data |> Array.filter (fun x -> not x.IsHq))

    member x.TakeHQ() =
        MarketAnalyzer(item, world, data |> Array.filter (fun x -> x.IsHq))

    /// 默认25%市场容量
    member x.TakeVolume() = x.TakeVolume(25)

    member x.TakeVolume(cutPct: int) =
        MarketAnalyzer(
            item,
            world,
            [| let samples = data |> Array.sortBy (fun x -> x.Price)

               let itemCount = data |> Array.sumBy (fun x -> x.Quantity)

               let cutLen = itemCount * cutPct / 100
               let mutable rest = cutLen

               match itemCount = 0, cutLen = 0 with
               | true, _ -> ()
               | false, true ->
                   //返回第一个
                   yield data.[0]
               | false, false ->
                   for record in samples do
                       let takeCount = min rest record.Quantity

                       if takeCount <> 0 then
                           rest <- rest - takeCount
                           yield record |]
        )

type UniversalisRecord with
    member x.GetListingAnalyzer() =
        let data = x.Listings |> Array.map MarketData.Order

        let info = x.GetInfo()
        MarketAnalyzer(info.Item, info.World, data)

    member x.GetTradeLogAnalyzer() =
        let data = x.TradeLogs |> Array.map MarketData.Trade

        let info = x.GetInfo()
        MarketAnalyzer(info.Item, info.World, data)

let MateriaGrades = [| "壹型"; "贰型"; "叁型"; "肆型"; "伍型"; "陆型"; "柒型"; "捌型" |]

let MateriaAliasMapper =
    let mapper = AliasMapper()
    mapper.Add("雄略", "信念", "信", "DET")
    mapper.Add("神眼", "直击", "直", "DH")
    mapper.Add("武略", "暴击", "暴", "CIT")
    mapper.Add("战技", "技能速度", "技速")
    mapper.Add("咏唱", "咏唱速度", "咏速")
    mapper.Add("刚柔", "坚韧", "TEN")
    mapper.Add("信力", "信仰", "PIE")
    mapper.Add("魔匠", "制作力", "CP")
    mapper.Add("巨匠", "加工精度", "加工")
    mapper.Add("名匠", "作业精度", "作业")
    mapper.Add("器识", "采集力", "GP", "采集")
    mapper.Add("达识", "获得力", "获得")
    mapper.Add("博识", "鉴别力", "鉴别")
    mapper
