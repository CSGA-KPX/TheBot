module InitCollection

open KPX.TheBot.Data.Common.Database

open NUnit.Framework

let mutable private initCollection = true

let Setup () =
    try
        if initCollection then
            BotDataInitializer.ClearCache()
            BotDataInitializer.ShrinkCache()
            BotDataInitializer.InitializeAllCollections()
            initCollection <- false
    with e -> failwithf "%O" e
