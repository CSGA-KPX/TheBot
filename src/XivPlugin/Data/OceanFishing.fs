module KPX.XivPlugin.Data.OceanFishing

open System

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache


let private RouteTable =
    let col = XivProvider.XivCollectionChs

    col.GetSheet("IKDRouteTable")
    |> Seq.map (fun row -> row.As<int>("Route"))
    |> Seq.toArray

let private RouteDefine =
    let col = XivProvider.XivCollectionChs

    [| for row in col.IKDRoute.TypedRows do
           let routeName = row.Name.AsString()

           let timeStr =
               match row.Time.AsInts().[0] with
               | 0 -> "占位" // 0是第一行，为了保证.[rid]操作，保留这一行了
               | 1 -> "黄昏"
               | 2 -> "黑夜"
               | 3 -> "白天"
               | o -> failwithf $"未知海钓时间：%i{o}"

           yield $"%s{routeName}_%s{timeStr}" |]

let private RefDate =
    DateTimeOffset.Parse("2020/2/21 20:00 +08:00")

let private RefDateOffset = 10

let rm = ResxManager("XivPlugin.XivOceanFishing")

let GetWindowMessage (key : string) =
    let msg = rm.GetString(key)

    if isNull msg then
        Array.singleton $"%s{key} 暂无攻略文本"
    else
        msg.Split([| "\r\n"; "\r"; "\n" |], StringSplitOptions.None)

let CalculateCooldown (now : DateTimeOffset) =
    // 进位到最近的CD
    let next, now =
        if (now.Hour % 2) = 0 && now.Minute <= 15 then
            false, now
        else // 错过了，调整到下一个CD
            let m = -now.Minute |> float
            let h = if now.Hour % 2 = 0 then 2.0 else 1.0
            true, now.AddMinutes(m).AddHours(h)

    let span = now - RefDate

    let offset =
        (int span.TotalHours) % (RouteTable.Length * 2)
        / 2

    let idx =
        (RefDateOffset + offset) % RouteTable.Length

    let rid = RouteTable.[idx]

    let msgKey = RouteDefine.[rid]

    {| IsNextCooldown = next
       CooldownDate = now
       RouTableId = idx
       RouteId = rid
       Message = GetWindowMessage(msgKey) |}

type OceanFishingTest() =
    inherit DataTest()
    
    override x.RunTest() =
         for i = 0 to 72 do
             CalculateCooldown(DateTimeOffset.Now.AddHours((float i) * 2.0))
             |> ignore