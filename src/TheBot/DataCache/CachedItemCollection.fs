namespace KPX.TheBot.Host.DataCache

open KPX.TheBot.Host.Data

open LiteDB


[<AbstractClass>]
type CachedItemCollection<'Key, 'Item>() =
    inherit BotDataCollection<'Key, 'Item>()

    /// 获取一个'Value，不经过不写入缓存
    abstract DoFetchItem : 'Key -> 'Item

    abstract IsExpired : 'Item -> bool

    /// 强制获得一个'Item，然后写入缓存
    member x.FetchItem(key : 'Key) =
        let item = x.DoFetchItem(key)
        x.DbCollection.Upsert(item) |> ignore
        item

    /// 获得一个'Item，如果有缓存优先拿缓存
    member x.GetItem(key : 'Key) =
        let ret =
            x.DbCollection.TryFindById(BsonValue(key))

        if ret.IsNone || x.IsExpired(ret.Value) then
            x.FetchItem(key)
        else
            ret.Value