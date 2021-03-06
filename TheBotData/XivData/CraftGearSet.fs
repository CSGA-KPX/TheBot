﻿namespace KPX.TheBot.Data.XivData.CraftGearSet

open System

open LiteDB

open KPX.TheBot.Data.Common.Database
open KPX.TheBot.Data.XivData


[<CLIMutable>]
type CraftableGear =
    { [<BsonId(false)>]
      /// ItemId
      Id : int
      ItemLv : int
      EquipSlotCategory : int
      ClassJobCategory : string }

type CraftableGearCollection private () =
    inherit CachedTableCollection<int, CraftableGear>(DefaultDB)

    static let instance = CraftableGearCollection()
    static member Instance = instance

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection

        db.EnsureIndex(LiteDB.BsonExpression.Create("_id"), true)
        |> ignore

        db.EnsureIndex(LiteDB.BsonExpression.Create("ItemLv"), false)
        |> ignore

        use col = BotDataInitializer.XivCollectionChs

        (*let fields =
            [| "EquipSlotCategory"
               "IsUntradable"
               "Level{Equip}"
               "ClassJobCategory"
               "Level{Item}"
               "CanBeHq"
               "IsAdvancedMeldingPermitted" |]

        let chs = col.GetSheet("Item", fields)*)

        let ClassJobCategory =
            seq {
                let sheet = col.GetSheet("ClassJobCategory")

                let jobs =
                    sheet.Header.Headers.[2..]
                    |> Array.map (fun x -> x.ColumnName)

                for row in sheet do
                    let j =
                        jobs
                        |> Array.filter (fun job -> row.As<bool>(job))

                    yield row.Key.Main, String.Join(" ", j)
            }
            |> readOnlyDict

        seq {
            for item in col.Item.TypedRows do
                let elv = item.``Level{Equip}``.AsInt()

                if (elv >= 80)
                   && ((elv % 10) = 0)
                   && (not <| item.IsUntradable.AsBool())
                   && (item.``Level{Item}``.AsInt() >= 340)
                   && (item.CanBeHq.AsBool())
                   && (item.IsAdvancedMeldingPermitted.AsBool()) then // 部分装备天书能给个5孔的华美型，此时会禁用禁断
                    yield
                        { Id = item.Key.Main
                          ItemLv = item.``Level{Item}``.AsInt()
                          EquipSlotCategory = item.EquipSlotCategory.AsInt()
                          ClassJobCategory = ClassJobCategory.[item.ClassJobCategory.AsInt()] }
        }
        |> db.InsertBulk
        |> ignore

    member x.TryLookupByItem(item : XivItem) = x.DbCollection.TryFindById(item.Id)

    member x.Search(iLv : int, jobCode : string) =
        let query =
            Query.And(
                Query.EQ("ItemLv", BsonValue(iLv)),
                Query.Contains("ClassJobCategory", jobCode)
            )

        [| for g in x.DbCollection.Find(query) do
               if g.EquipSlotCategory = 12 then
                   //戒指要多一个
                   yield g

               yield g |]
