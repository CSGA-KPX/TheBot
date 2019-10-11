module XivData.Test
open System


[<EntryPoint>]
let main args = 
    Utils.ClearDb() |> ignore

    Utils.InitAllDb() |> ignore

    Console.ReadLine() |> ignore
    0