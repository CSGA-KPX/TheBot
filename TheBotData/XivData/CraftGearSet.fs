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

    override x.Depends = [| typeof<CraftRecipeProvider> ; typeof<CompanyCraftRecipeProvider>|]

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection
        db.EnsureIndex(LiteDB.BsonExpression.Create("_id"), true) |> ignore
        db.EnsureIndex(LiteDB.BsonExpression.Create("ItemLv"), false) |> ignore

        let fields = [|"EquipSlotCategory"; "Level{Equip}"; "Name"; "ClassJobCategory"; "Level{Item}"|]
        let chs = BotDataInitializer.GetXivCollectionChs().GetSheet("Item", fields)
        
        let ClassJobCategory = 
            [|
                let sheet = BotDataInitializer.GetXivCollectionChs().GetSheet("ClassJobCategory")
                let jobs = 
                    sheet.Header.Headers.[2..]
                    |> Array.map (fun x -> x.ColumnName)
                for row in sheet do 
                    let j = 
                        jobs |> Array.filter (fun job -> row.As<bool>(job))
                    yield row.Key.Main, String.Join(" ", j)
            |] |> readOnlyDict
        
        let rm = XivRecipeManager.Instance
        let ic = ItemCollection.Instance
        seq {
            for item in chs do 
                let eq = item.As<int>("EquipSlotCategory") <> 0
                let le = (item.As<int>("Level{Equip}") % 10) = 0
                if eq && le then
                    let cf = 
                        let item = ic.GetByKey(item.Key.Main)
                        let recipe = rm.TryGetRecipe(item)
                        recipe.IsSome && recipe.Value.Input.Length > 0
                    if cf then
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