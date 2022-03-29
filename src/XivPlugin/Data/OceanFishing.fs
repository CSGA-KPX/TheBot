module KPX.XivPlugin.Data.OceanFishing

open System

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache

open KPX.XivPlugin
open KPX.XivPlugin.Data


[<RequireQualifiedAccess>]
module private China =
    let RouteTable =
        let col = ChinaDistroData.GetCollection()

        col.GetSheet("IKDRouteTable")
        |> Seq.map (fun row -> row.As<int>("Route"))
        |> Seq.toArray

    let RouteDefine =
        let col = ChinaDistroData.GetCollection()

        [| for row in col.IKDRoute do
               let routeName = row.Name.AsString()

               let timeStr =
                   match row.Time.AsInts().[0] with
                   | 0 -> "占位" // 0是第一行，为了保证.[rid]操作，保留这一行了
                   | 1 -> "黄昏"
                   | 2 -> "黑夜"
                   | 3 -> "白天"
                   | o -> failwithf $"未知海钓时间：%i{o}"

               yield $"%s{routeName}_%s{timeStr}" |]

[<RequireQualifiedAccess>]
module private Offical =
    let RouteTable =
        let col = OfficalDistroData.GetCollection()

        col.GetSheet("IKDRouteTable")
        |> Seq.map (fun row -> row.As<int>("Route"))
        |> Seq.toArray

    let RouteDefine =
        let col = OfficalDistroData.GetCollection()

        [| for row in col.IKDRoute do
               let routeName = row.Name.AsString()

               let timeStr =
                   match row.Time.AsInts().[0] with
                   | 0 -> "占位" // 0是第一行，为了保证.[rid]操作，保留这一行了
                   | 1 -> "黄昏"
                   | 2 -> "黑夜"
                   | 3 -> "白天"
                   | o -> failwithf $"未知海钓时间：%i{o}"

               yield $"%s{routeName}_%s{timeStr}" |]

let private getRouteTable =
    function
    | VersionRegion.China -> China.RouteTable
    | VersionRegion.Offical -> Offical.RouteTable

let private getRouteDefine =
    function
    | VersionRegion.China -> China.RouteDefine
    | VersionRegion.Offical -> Offical.RouteDefine

// 实测值
let private refDate = DateTimeOffset.Parse("2020/2/21 20:00 +08:00")
// 实测值
let private refDateOffset = 10

let private rm = ResxManager("XivPlugin.XivOceanFishing")

let GetWindowMessage (key: string) =
    let msg = rm.GetString(key)

    if isNull msg then
        Array.singleton $"%s{key} 暂无攻略文本"
    else
        msg.Split([| "\r\n"; "\r"; "\n" |], StringSplitOptions.None)

let CalculateCooldown (now: DateTimeOffset, region) =
    // 进位到最近的CD
    let next, now =
        if (now.Hour % 2) = 0 && now.Minute <= 15 then
            false, now
        else // 错过了，调整到下一个CD
            let m = -now.Minute |> float
            let h = if now.Hour % 2 = 0 then 2.0 else 1.0
            true, now.AddMinutes(m).AddHours(h)

    let span = now - refDate
    let routeTable = getRouteTable (region)
    let offset = (int span.TotalHours) % (routeTable.Length * 2) / 2
    let idx = (refDateOffset + offset) % routeTable.Length
    let rid = routeTable.[idx]
    let msgKey = (getRouteDefine region).[rid]

    {| IsNextCooldown = next
       CooldownDate = now
       RouTableId = idx
       RouteId = rid
       Message = GetWindowMessage(msgKey) |}


type OceanFishingTest() =
    inherit DataTest()

    override x.RunTest() =
        for i = 0 to 72 do
            let time = DateTimeOffset.Now.AddHours((float i) * 2.0)
            CalculateCooldown(time, VersionRegion.Offical) |> ignore
            CalculateCooldown(time, VersionRegion.China) |> ignore
