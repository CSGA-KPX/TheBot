namespace KPX.TheBot.Host.DataCache

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache.LiteDb

open LiteDB


[<AbstractClass>]
type CachedItemCollection<'Key, 'Item>(colName) =
    inherit BotDataCollection<'Item>(colName)

    new() = CachedItemCollection<'Key, 'Item>(System.String.Empty)

    /// 获取一个'Value，不经过不写入缓存
    abstract DoFetchItem: 'Key -> 'Item

    abstract IsExpired: 'Item -> bool

    /// 把相关数据载入数据库
    member x.LoadItems(items : 'Item seq) =
        x.DbCollection.Upsert(items) |> ignore

    /// 获得一个'Item，如果有缓存优先拿缓存
    member x.GetItem(key: 'Key) =
        let ret = Query.EQ("_id", BsonValue(key)) |> x.DbCollection.TryFindOne

        if ret.IsNone || x.IsExpired(ret.Value) then
            let item = x.DoFetchItem(key)
            x.DbCollection.Upsert(item) |> ignore
            item
        else
            ret.Value
