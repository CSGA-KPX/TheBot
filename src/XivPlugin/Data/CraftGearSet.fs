namespace KPX.XivPlugin.Data

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb

open KPX.XivPlugin
open KPX.XivPlugin.Data

open LiteDB


[<CLIMutable>]
type CraftableGear =
    { [<BsonId>]
      LiteDbId : int
      Region : VersionRegion
      ItemId: int
      ItemLv: int
      EquipSlotCategory: int
      ClassJobCategory: string }

[<AbstractClass>]
type CraftableGearCollection private () =
    inherit CachedTableCollection<CraftableGear>()

    //static member val Instance = CraftableGearCollection()

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        // 不知道怎么算一套装备，先放着不写了
        x.DbCollection.EnsureIndex(fun x -> x.ItemLv) |> ignore
        ()

    member x.TryLookupByItem(item: XivItem) = x.DbCollection.TryFindById(item.ItemId)

    member x.Search(iLv: int, jobCode: string) =
        let query = Query.And(Query.EQ("ItemLv", BsonValue(iLv)), Query.Contains("ClassJobCategory", jobCode))

        [| for g in x.DbCollection.Find(query) do
               if g.EquipSlotCategory = 12 then
                   //戒指要多一个
                   yield g

               yield g |]