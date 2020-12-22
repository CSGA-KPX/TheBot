module XivMiscText

open System

open KPX.TheBot.Data.XivData

open NUnit.Framework


[<Test>]
let ``FFXIV : OceanFishing function`` () =
    for i = 0 to 72 do
        OceanFishing.CalculateCooldown(DateTimeOffset.Now.AddHours((float i) * 2.0))
        |> ignore

[<Test>]
let ``FFXIV : World function`` () = World.WorldFromName.["拉诺西亚"] |> ignore
