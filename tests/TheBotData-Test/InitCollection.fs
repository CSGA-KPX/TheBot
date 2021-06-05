module InitCollection

open System

open KPX.TheBot.Data.Common.Database


let mutable private initCollection = true

let Setup () =
    try
        if initCollection then
            Environment.CurrentDirectory <- __SOURCE_DIRECTORY__ + "/../../build/staticData/"
            BotDataInitializer.ClearCache()
            BotDataInitializer.ShrinkCache()
            BotDataInitializer.InitializeAllCollections()
            initCollection <- false
    with e -> failwithf "%O" e
