module BotData.XivData.OceanFishing
open System

open BotData.Common.Resource

let private RouteTable = 
    [|1; 4; 2; 5; 3; 6; 1; 4; 2; 5; 3; 6; 4; 1; 5; 2; 6; 3; 4; 1; 5; 2; 6; 3;
        2; 5; 3; 6; 1; 4; 2; 5; 3; 6; 1; 4; 5; 2; 6; 3; 4; 1; 5; 2; 6; 3; 4; 1;
        3; 6; 1; 4; 2; 5; 3; 6; 1; 4; 2; 5; 6; 3; 4; 1; 5; 2; 6; 3; 4; 1; 5; 2|]

let private RouteDefine = 
    [|
        1, "梅尔托尔海峡北_白天"
        2, "梅尔托尔海峡北_黄昏"
        3, "梅尔托尔海峡北_黑夜"
        4, "罗塔诺海海面_白天"
        5, "罗塔诺海海面_黄昏"
        6, "罗塔诺海海面_黑夜"
    |] |> readOnlyDict

let private RefDate = DateTimeOffset.Parse("2020/2/21 20:00 +08:00")
let private RefDateOffset = 10

let rm = GetResourceManager("XivOceanFishing")

let CalculateCooldown (now : DateTimeOffset) =
    // 进位到最近的CD
    let (next, now) = 
        if (now.Hour % 2) = 0 && now.Minute <= 15 then
            false, now
        else // 错过了，调整到下一个CD
            let m = -now.Minute |> float
            let h = if now.Hour % 2 = 0 then 2.0 else 1.0
            true, now.AddMinutes(m).AddHours(h)

    let span = now - RefDate

    let offset = (int span.TotalHours) % (RouteTable.Length * 2) / 2
    let idx = (RefDateOffset + offset) % RouteTable.Length
    let rid = RouteTable.[idx]

    let msgKey = RouteDefine.[rid]
    let message = rm.GetString(msgKey)
    if isNull message then
        failwithf "发生错误：message是null, key是%s" msgKey

    {|  IsNextCooldown = next
        CooldownDate = now
        RouTableId = idx
        RouteId = rid
        Message = message.Split([|"\r\n"; "\r"; "\n"|], StringSplitOptions.None)   |}