namespace KPX.XivPlugin.Data


open System

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb

open KPX.XivPlugin
open KPX.XivPlugin.Data

open LiteDB


[<CLIMutable>]
type CraftableGear =
    { [<BsonId>]
      LiteDbId: int
      ItemId: int
      ItemLv: int
      EquipSlotCategory: int
      ClassJobCategory: string }

/// 相关数据以国际服为准
type CraftableGearCollection private () =
    inherit CachedTableCollection<CraftableGear>()

    /// 最低工匠等级
    let minCraftLevel = 60

    static member val Instance = CraftableGearCollection()

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        // 不知道怎么算一套装备，先放着不写了
        x.DbCollection.EnsureIndex(fun x -> x.ItemLv) |> ignore

        let col = OfficalDistroData.GetCollection()

        let ClassJobCategory =
            seq {

                let sht = col.ClassJobCategory
                let header = sht.Header.Headers

                for cat in sht.TypedRows do
                    let jobs =
                        Seq.zip header cat.RawData
                        |> Seq.choose (fun (hdr, value) ->
                            if value = "True" then
                                Some(hdr.ColumnName)
                            else
                                None)

                    yield cat.Key.Main, String.Join(" ", jobs)
            }
            |> readOnlyDict

        seq {
            for row in col.Recipe.TypedRows do
                let recipeLv = row.RecipeLevelTable.AsRow().ClassJobLevel.AsInt()

                if recipeLv >= minCraftLevel then
                    let retItem = row.``Item{Result}``.AsRow()
                    let cond = retItem.CanBeHq.AsBool() && retItem.IsAdvancedMeldingPermitted.AsBool()

                    if cond then
                        yield
                            { LiteDbId = 0
                              ItemId = retItem.Key.Main
                              ItemLv = retItem.``Level{Item}``.AsInt()
                              EquipSlotCategory = retItem.EquipSlotCategory.AsInt()
                              ClassJobCategory = ClassJobCategory.[retItem.ClassJobCategory.AsInt()] }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.TryLookupByItem(item: XivItem) =
        x.DbCollection.TryFindOne(Query.EQ("ItemId", BsonValue(item.ItemId)))

    member x.Search(iLv: int, jobCode: string) =
        let query = Query.And(Query.EQ("ItemLv", BsonValue(iLv)), Query.Contains("ClassJobCategory", jobCode))

        [| for g in x.DbCollection.Find(query) do
               if g.EquipSlotCategory = 12 then
                   //戒指要多一个
                   yield g

               yield g |]
