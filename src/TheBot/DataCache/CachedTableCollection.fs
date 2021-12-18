namespace rec KPX.TheBot.Host.DataCache

open System

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache.LiteDb

open LiteDB


[<CLIMutable>]
type TableUpdateTime =
    { [<BsonId(false)>]
      Id: string
      Updated: DateTimeOffset }

type internal TableUpdateInfo private () =

    static let updateCol =
        DataAgent
            .GetCacheDatabase()
            .GetCollection<TableUpdateTime>()

    /// 记录CachedTableCollection<>的更新时间
    static member RegisterCollectionUpdate(col: CachedTableCollection<_, _>) =
        let record =
            { Id = col.GetType().Name
              Updated = DateTimeOffset.Now }

        updateCol.Upsert(record) |> ignore

    static member GetCollectionUpdateTime(col: CachedTableCollection<_, _>) =
        let ret = updateCol.FindById(BsonValue(col.GetType().Name))

        if isNull (box ret) then
            DateTimeOffset.MinValue
        else
            ret.Updated

[<AbstractClass>]
type CachedTableCollection<'Key, 'Item>(colName) =
    inherit BotDataCollection<'Key, 'Item>(colName)

    let updateLock = obj ()

    new () as x = CachedTableCollection<'Key, 'Item>(x.GetType().Name)

    abstract IsExpired: bool

    /// 处理数据并添加到数据库，建议在事务内处理
    abstract InitializeCollection: unit -> unit

    member x.CheckUpdate() =
        lock
            updateLock
            (fun () ->
                if x.IsExpired then
                    x.Clear()
                    x.InitializeCollection()
                    x.RegisterCollectionUpdate())

    member x.RegisterCollectionUpdate() =
        TableUpdateInfo.RegisterCollectionUpdate(x)

    member x.GetLastUpdateTime() =
        TableUpdateInfo.GetCollectionUpdateTime(x)
