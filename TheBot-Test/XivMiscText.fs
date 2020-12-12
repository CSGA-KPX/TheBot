module XivMiscText

open System

open BotData.XivData

open NUnit.Framework


[<Test>]
let ``FFXIV : OceanFishing function`` () = 
    Assert.DoesNotThrow(fun () ->
        OceanFishing.CalculateCooldown(DateTimeOffset.Now)
        |> ignore
    )