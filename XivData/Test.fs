module XivData.Test
open System


let RebuildDb () = 
    printfn "清空数据库"
    Utils.ClearDb() |> ignore
    printfn "重建数据库"
    Utils.InitAllDb() |> ignore

[<EntryPoint>]
let main args = 
    Utils.ClearDb() |> ignore

    Utils.InitAllDb() |> ignore

    Console.ReadLine() |> ignore
    0