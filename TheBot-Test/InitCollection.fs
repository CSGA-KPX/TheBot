module InitCollection

open BotData.Common.Database

open NUnit.Framework

let mutable private initCollection = false

let Setup () =
    try
        if initCollection then
            BotDataInitializer.ClearCache()
            BotDataInitializer.ShrinkCache()
            BotDataInitializer.InitializeAllCollections()
            initCollection <- false
    with
    | e -> 
        failwithf "%O" e