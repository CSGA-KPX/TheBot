module InitCollection

open System

open KPX.TheBot.Data.Common.Database


let mutable private initCollection = true

let Setup () =
    try
        if initCollection then
            Environment.CurrentDirectory <- @"K:\Source\Repos\TheBot\TheBot\bin\Debug\net5.0"
            BotDataInitializer.ClearCache()
            BotDataInitializer.ShrinkCache()
            BotDataInitializer.InitializeAllCollections()
            initCollection <- false
    with e -> failwithf "%O" e
