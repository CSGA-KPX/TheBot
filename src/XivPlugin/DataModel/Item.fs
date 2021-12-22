namespace KPX.XivPlugin.DataModel

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb

open KPX.XivPlugin

open LiteDB


[<CLIMutable>]
[<Struct>]
type XivItem =
    { [<BsonId>]
      LiteDbId: int
      ItemId: int
      Region: VersionRegion
      Name: string }

    /// <summary>
    /// 转换为 区域/名称(id) 格式
    /// </summary>
    override x.ToString() = $"%A{x.Region}/%s{x.Name}(%i{x.ItemId})"

[<Sealed>]
type ItemCollection private () =
    inherit CachedTableCollection<XivItem>()

    static member val Instance = ItemCollection()

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection

        db.EnsureIndex(fun x -> x.ItemId) |> ignore
        db.EnsureIndex(fun x -> x.Region) |> ignore

        seq {
            use col = ChinaDistroData.GetCollection()

            for row in col.Item.TypedRows do
                yield
                    { LiteDbId = 0
                      ItemId = row.Key.Main
                      Region = VersionRegion.China
                      Name = row.Name.AsString() }

            use col = OfficalDistroData.GetCollection()

            for row in col.Item.TypedRows do
                yield
                    { LiteDbId = 0
                      ItemId = row.Key.Main
                      Region = VersionRegion.Offical
                      Name = row.Name.AsString() }
        }
        |> db.InsertBulk
        |> ignore

    /// <summary>
    /// 根据id查找物品
    /// </summary>
    /// <param name="id">ItemId</param>
    /// <param name="region">版本区</param>
    member x.GetByItemId(id: int, region: VersionRegion) =
        let ret = x.DbCollection.TryQueryOne(fun x -> x.ItemId = id && x.Region = region)

        x.PassOrRaise(ret, "找不到物品:{0}", id)

    /// <summary>
    /// 根据id查找物品
    /// </summary>
    /// <param name="id">ItemId</param>
    /// <param name="region">版本区</param>
    member x.TryGetByItemId(id: int, region: VersionRegion) =
        x.DbCollection.TryQueryOne(fun x -> x.ItemId = id && x.Region = region)

    /// <summary>
    /// 根据名称匹配道具
    /// </summary>
    /// <param name="name">物品名</param>
    /// <param name="region">版本区</param>
    member x.TryGetByName(name: string, region: VersionRegion) =
        x.DbCollection.TryQueryOne(fun x -> x.Name = name && x.Region = region)

    /// <summary>
    /// 查找名称包含指定字符的物品。默认最多返回50个。
    /// </summary>
    /// <param name="str">查找字符</param>
    /// <param name="region">查找版本区</param>
    /// <param name="limit">上限</param>
    member x.SearchByName(str, region: VersionRegion, ?limit: int) =
        x.DbCollection.Find(Query.Contains("Name", str))
        |> Seq.filter (fun x -> x.Region = region)
        |> Seq.truncate (defaultArg limit 50)
        |> Seq.toArray

    interface IDataTest with
        member x.RunTest() =
            let i = ItemCollection.Instance

            // 中国服
            Expect.equal (i.GetByItemId(4, VersionRegion.China).Name) "风之碎晶"

            let ret = i.TryGetByName("风之碎晶", VersionRegion.China)

            Expect.isSome ret
            Expect.equal ret.Value.Name "风之碎晶"
            Expect.equal ret.Value.LiteDbId 4

            // 国际服/日区
            Expect.equal (i.GetByItemId(4, VersionRegion.Offical).Name) "ウィンドシャード"

            let ret = i.TryGetByName("ウィンドシャード", VersionRegion.Offical)

            Expect.isSome ret
            Expect.equal ret.Value.Name "ウィンドシャード"
            Expect.equal ret.Value.LiteDbId 4
