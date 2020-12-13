module XivMiscText

open System

open BotData.XivData

open NUnit.Framework


[<Test>]
let ``FFXIV : OceanFishing function`` () = 
    OceanFishing.CalculateCooldown(DateTimeOffset.Now)
    |> ignore

[<Test>]
let ``FFXIV : World function`` () = 
    World.WorldFromName.["拉诺西亚"] |> ignore