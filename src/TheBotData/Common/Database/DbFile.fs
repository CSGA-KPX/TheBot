namespace rec KPX.TheBot.Data.Common.Database

open System.Collections.Generic

open LiteDB

open KPX.TheBot.Data.Common.Resource


[<AutoOpen>]
module Helpers =
    let private dbCache = Dictionary<string, LiteDatabase>()

    let getLiteDB (name : string) =
        if not <| dbCache.ContainsKey(name) then
            let path = GetStaticFile(name)
            let dbFile = $"Filename=%s{path};"
            let db = new LiteDatabase(dbFile)
            dbCache.Add(name, db)

        dbCache.[name]

    [<Literal>]
    let internal DefaultDB = "BotDataCache.db"

    do
        BsonMapper.Global.EmptyStringToNull <- false
        BsonMapper.Global.EnumAsInteger <- true