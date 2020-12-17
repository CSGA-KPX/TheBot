namespace BotData.XivData.CraftGearSet

open System

open LiteDB

open BotData.Common.Database
open BotData.XivData.Item
open BotData.XivData.Recipe

[<CLIMutable>]
type CraftableGear = 
    {
        [<BsonId(false)>]
        /// ItemId
        Id     : int
        ItemLv : int
        EquipSlotCategory : int
        ClassJobCategory : string
    }

type CraftableGearCollection private () =
    inherit  CachedTableCollection<int, CraftableGear>()

    static let instance = CraftableGearCollection()
    static member Instance = instance

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection
        db.EnsureIndex(LiteDB.BsonExpression.Create("_id"), true) |> ignore
        db.EnsureIndex(LiteDB.BsonExpression.Create("ItemLv"), false) |> ignore

        let fields = [| "EquipSlotCategory"; "IsUntradable";
                        "Level{Equip}"; "ClassJobCategory";
                        "Level{Item}"; "CanBeHq"; "IsAdvancedMeldingPermitted"|]
        use col = BotDataInitializer.XivCollectionChs
        let chs = col.GetSheet("Item", fields)
        
        let ClassJobCategory = 
            [|
                let sheet = col.GetSheet("ClassJobCategory")
                let jobs = 
                    sheet.Header.Headers.[2..]
                    |> Array.map (fun x -> x.ColumnName)
                for row in sheet do 
                    let j = 
                        jobs |> Array.filter (fun job -> row.As<bool>(job))
                    yield row.Key.Main, String.Join(" ", j)
            |] |> readOnlyDict
        
        seq {
            for item in chs do 
                let elv = item.As<int>("Level{Equip}")
                if (elv >= 80)
                    && ((elv % 10) = 0)
                    && (not <| item.As<bool>("IsUntradable"))
                    && (item.As<int>("Level{Item}") >= 340 )
                    && (item.As<bool>("CanBeHq")) 
                    && (item.As<bool>("IsAdvancedMeldingPermitted")) then // 部分装备天书能给个5孔的华美型，此时会禁用禁断
                    yield {
                        Id = item.Key.Main
                        ItemLv = item.As<int>("Level{Item}")
                        EquipSlotCategory = item.As<int>("EquipSlotCategory")
                        ClassJobCategory = ClassJobCategory.[item.As<int>("ClassJobCategory")]
                    }
        }
        |> db.InsertBulk
        |> ignore

    member x.TryLookupByItem(item : ItemRecord) = x.TryGetByKey(item.Id)

    member x.Search(iLv : int, jobCode : string) = 
        let query = 
            Query.And(Query.EQ("ItemLv", BsonValue(iLv)),
                        Query.Contains("ClassJobCategory", jobCode))
        [|
            for g in x.DbCollection.Find(query) do 
                if g.EquipSlotCategory = 12 then
                    //戒指要多一个
                    yield g
                yield g
        |]