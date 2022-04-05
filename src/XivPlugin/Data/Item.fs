namespace KPX.XivPlugin.Data

open System
open System.Collections.Generic

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb

open LiteDB


[<CLIMutable>]
type XivItem =
    { [<BsonId(false)>]
      ItemId: int
      ChineseName: string
      OfficalName: string
      PatchNumber: PatchNumber
      CanHq: bool }

    /// <summary>
    /// 转换为 区域/名称(id) 格式
    /// </summary>
    override x.ToString() =
        $"(%i{x.ItemId}) : %A{x.ChineseName}/%s{x.OfficalName}"

    /// <summary>
    /// 优先中文，如果没有就日文
    /// </summary>
    [<BsonIgnore>]
    member x.DisplayName =
        match String.IsNullOrWhiteSpace(x.ChineseName), String.IsNullOrWhiteSpace(x.OfficalName) with
        | false, _ -> x.ChineseName
        | true, false -> x.OfficalName
        | true, true -> "NoName"

[<Sealed>]
type ItemCollection private () =
    inherit CachedTableCollection<XivItem>()

    static member val Instance = ItemCollection()

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        x.DbCollection.EnsureIndex(fun x -> x.ChineseName) |> ignore
        x.DbCollection.EnsureIndex(fun x -> x.OfficalName) |> ignore

        seq {
            let cDict = Dictionary<int, string>()

            for row in ChinaDistroData.GetCollection().Item do
                cDict.Add(row.Key.Main, row.Name.AsString())

            for row in OfficalDistroData.GetCollection().Item do
                let succ, cName = cDict.TryGetValue(row.Key.Main)

                let patchNumber = ItemPatchDifference.ToPatchNumber(row.Key.Main)

                { ItemId = row.Key.Main
                  ChineseName = if succ then cName else String.Empty
                  OfficalName = row.Name.AsString()
                  PatchNumber = patchNumber
                  CanHq = row.CanBeHq.AsBool() }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    /// <summary>
    /// 根据id查找物品
    /// </summary>
    /// <param name="id">ItemId</param>
    /// <param name="region">版本区</param>
    member x.GetByItemId(id: int) =
        let ret = x.DbCollection.TryFindOne(Query.EQ("_id", id))

        x.PassOrRaise(ret, "找不到物品:{0}", id)

    /// <summary>
    /// 根据id查找物品
    /// </summary>
    /// <param name="id">ItemId</param>
    /// <param name="region">版本区</param>
    member x.TryGetByItemId(id: int) =
        x.DbCollection.TryFindOne(Query.EQ("_id", id))

    /// <summary>
    /// 根据名称匹配道具
    /// </summary>
    /// <param name="name">物品名</param>
    /// <param name="region">版本区</param>
    member x.TryGetByName(name: string) =
        Query.Or(Query.EQ("ChineseName", name), Query.EQ("OfficalName", name))
        |> x.DbCollection.TryFindOne

    /// <summary>
    /// 查找名称包含指定字符的物品。默认最多返回50个。
    /// </summary>
    /// <param name="str">查找字符</param>
    /// <param name="region">查找版本区</param>
    /// <param name="limit">上限</param>
    member x.SearchByName(str, ?limit: int) =
        let query = Query.Or(Query.Contains("ChineseName", str), Query.Contains("OfficalName", str))
        x.DbCollection.Find(query) |> Seq.truncate (defaultArg limit 50) |> Seq.toArray

    interface IDataTest with
        member x.RunTest() =
            let i = ItemCollection.Instance

            Expect.equal (i.GetByItemId(4).ChineseName) "风之碎晶"
            Expect.equal (i.GetByItemId(4).OfficalName) "ウィンドシャード"
