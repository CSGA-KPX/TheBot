module KPX.TheBot.Data.Program

open KPX.TheBot.Data.Common


[<EntryPoint>]
let main _ =
    try
        Database.BotDataInitializer.ClearCache()
        Database.BotDataInitializer.ShrinkCache()
        Database.BotDataInitializer.InitializeAllCollections()
        Database.BotDataInitializer.RunTests()
        0
    with
    | e ->
        printfn $"{e}"
        1