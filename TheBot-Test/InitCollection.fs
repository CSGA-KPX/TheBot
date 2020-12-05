module InitCollection

open BotData.Common.Database

let mutable private initCollection = true

let Setup () =
    try
        if initCollection then
            BotDataInitializer.ClearCache()
            BotDataInitializer.ShrinkCache()
            BotDataInitializer.InitializeAllCollections()
            initCollection <- false
    with
    | e -> printfn "%O" e; reraise()