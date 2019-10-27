﻿module XivData.Recipe
open System
open System.Collections.Generic
open LibFFXIV.GameData.Raw
open XivData.Item

[<CLIMutable>]
type RecipeRecord = 
    {
        [<LiteDB.BsonIdAttribute(false)>]
        Id            : int
        ResultItem    : ItemRecord
        ProductCount  : float
        Materials     : (ItemRecord * float) []
    }

type FinalMaterials () = 
    let m = new Dictionary<ItemRecord, float>()

    member x.AddOrUpdate(item, runs) = 
        if m.ContainsKey(item) then
            m.[item] <- m.[item] + runs
        else
            m.Add(item, runs)

    member x.Get() = 
        [|
            for kv in m do 
                yield (kv.Key, kv.Value)
        |]

type IRecipeProvider = 
    abstract TryGetRecipe : ItemRecord -> RecipeRecord option


type CraftRecipeProvider private () =
    inherit Utils.XivDataSource()
    let colName = "CraftRecipeProvider"
    let exists = Utils.Db.CollectionExists(colName)
    let db = Utils.Db.GetCollection<RecipeRecord>(colName)
    do
        db.EnsureIndex("_id", true) |> ignore
        db.EnsureIndex("ResultItem.Id") |> ignore
        if not exists then
            //build from scratch
            let db = Utils.Db.GetCollection<RecipeRecord>(colName)
            printfn "Building CraftRecipeProvider"
            let col = new LibFFXIV.GameData.Raw.XivCollection(XivLanguage.ChineseSimplified) :> IXivCollection
            let lookup id = Item.ItemCollection.Instance.LookupById(id).Value
            seq {
                for kv in col.GetSheet("Recipe") do
                    let row = kv.Value
                    let itemsKeys = 
                        row.AsArray<XivSheetReference>("Item{Ingredient}", 10)
                        |> Array.map (fun x -> x.Key)
                    let amounts = 
                        row.AsArray<byte>("Amount{Ingredient}", 10)
                        |> Array.map (fun x -> float x)
                    let materials = 
                        Array.zip itemsKeys amounts
                        |> Array.filter (fun (id,_) -> id > 0)
                        |> Array.map (fun (id, runs) -> (lookup id, runs))
                    yield {
                            Id = row.Key
                            ResultItem = row.As<XivSheetReference>("Item{Result}").Key |> lookup
                            ProductCount = row.As<byte>("Amount{Result}") |> float
                            Materials = materials
                        }
            }  |> db.InsertBulk |> ignore
            GC.Collect()
            

    static let instance = new CraftRecipeProvider()
    static member Instance = instance

    interface IRecipeProvider with
        override x.TryGetRecipe(item) = 
            let id = new LiteDB.BsonValue(item.Id)
            let ret = db.FindOne(LiteDB.Query.EQ("ResultItem.Id", id))
            if isNull (box ret) then
                None
            else
                Some ret

type CompanyCraftRecipeProvider private () =
    inherit Utils.XivDataSource()
    let colName = "CompanyCraftRecipeProvider"
    let exists = Utils.Db.CollectionExists(colName)
    let db = Utils.Db.GetCollection<RecipeRecord>(colName)
    do
        db.EnsureIndex("_id", true) |> ignore
        db.EnsureIndex("ResultItem.Id") |> ignore
        if not exists then
            //build from scratch
            let db = Utils.Db.GetCollection<RecipeRecord>(colName)
            printfn "Building CompanyCraftRecipeProvider"
            let col = new LibFFXIV.GameData.Raw.XivCollection(XivLanguage.ChineseSimplified) :> IXivCollection
            let lookup id = Item.ItemCollection.Instance.LookupById(id).Value
            seq {
                for kv in col.GetSheet("CompanyCraftSequence") do 
                    let ccs = kv.Value
                    let materials = 
                        [|
                            for part in ccs.AsRowArray("CompanyCraftPart", 8) do 
                                for proc in part.AsRowArray("CompanyCraftProcess", 3) do 
                                    let itemsKeys = 
                                        proc.AsRowArray("SupplyItem", 12, false)
                                        |> Array.map (fun r -> r.As<XivSheetReference>("Item").Key)
                                    let amounts = 
                                        let setAmount = proc.AsArray<uint16>("SetQuantity", 12)
                                        let setCount  = proc.AsArray<uint16>("SetsRequired", 12)
                                        setAmount
                                        |> Array.map2 (fun a b -> a * b |> float ) setCount
                                    let materials = 
                                        Array.zip itemsKeys amounts
                                        |> Array.filter (fun (id,_) -> id > 0)
                                        |> Array.map (fun (id, runs) -> (lookup id, runs))
                                    yield! materials
                        |]
                    yield {
                            Id = ccs.Key
                            ResultItem = ccs.As<XivSheetReference>("ResultItem").Key |> lookup
                            ProductCount = 1.0
                            Materials = materials
                        }
            }  |> db.InsertBulk |> ignore
            GC.Collect()

    static let instance = new CompanyCraftRecipeProvider()
    static member Instance = instance

    interface IRecipeProvider with
        override x.TryGetRecipe(item) = 
            let id = new LiteDB.BsonValue(item.Id)
            let ret = db.FindOne(LiteDB.Query.EQ("ResultItem.Id", id))
            if isNull (box ret) then
                None
            else
                Some ret

type RecipeManager private () = 
    let providers = HashSet<IRecipeProvider>()
    let rec findRecipe (list : IRecipeProvider list, item : ItemRecord) = 
        match list with 
        | [] -> None
        | head :: tail -> 
            let h = head.TryGetRecipe(item) 
            if h.IsSome then
                h
            else
                findRecipe(tail, item)


    static let instance = 
        let rm = new RecipeManager()
        rm.AddProvider(CraftRecipeProvider.Instance)
        rm.AddProvider(CompanyCraftRecipeProvider.Instance)
        rm

    static member GetInstance() = instance

    /// 生产一个流程的材料
    member x.GetMaterials(item : ItemRecord) =
        let recipe = (x :> IRecipeProvider).TryGetRecipe(item)
        [|
            if recipe.IsSome then
                yield! recipe.Value.Materials
        |]

    /// 生产一个成品的材料
    member x.GetMaterialsOne(item : ItemRecord) =
        let recipe = (x :> IRecipeProvider).TryGetRecipe(item)
        [|
            if recipe.IsSome then
                let recipeYield = recipe.Value.ProductCount
                let final = 
                    recipe.Value.Materials
                    |> Array.map (fun (item, count) -> (item, count / recipeYield))
                yield! final
        |]

    ///获取物品基本材料
    member x.GetMaterialsRec(item : ItemRecord) =
        let rec getMaterialsRec(acc : Dictionary<ItemRecord, (ItemRecord * float)>, item : ItemRecord, runs : float) = 
            let recipe = (x :> IRecipeProvider).TryGetRecipe(item)
            if recipe.IsNone then
                if acc.ContainsKey(item) then
                    let (item, count) = acc.[item]
                    acc.[item] <- (item, count + runs)
                else
                    acc.Add(item, (item, runs))
            else
                let realRuns  = runs / recipe.Value.ProductCount
                let materials = recipe.Value.Materials |> Array.map (fun (item, count) -> (item, count * realRuns))
                for (item, count) in materials do
                    getMaterialsRec(acc, item, count)
        [|
            let dict = new Dictionary<ItemRecord, (ItemRecord * float)>()
            getMaterialsRec(dict, item, 1.0)
            let ma = dict.Values |> Seq.toArray
            yield! ma
        |]

    ///获取物品以及子物品的直接材料
    member x.GetMaterialsRecGroup(item : ItemRecord) = 
        let rec getMaterialsRec(acc : Queue<string * (ItemRecord * float) []>, level : string, item : ItemRecord, runs : float) = 
            let recipe = (x :> IRecipeProvider).TryGetRecipe(item)
            if recipe.IsNone then
                ()
            else
                let realRuns  = runs / recipe.Value.ProductCount
                let self = level + "*" + String.Format("{0:0.###}", 1.0), [| (item, 1.0) |]
                acc.Enqueue(self)
                let materials = recipe.Value.Materials |> Array.map (fun (item, count) -> (item, count * realRuns))
                let countStr = "*" + String.Format("{0:0.###}", 1.0)
                acc.Enqueue(level + countStr + "/", materials)
                for (item, count) in materials do
                    getMaterialsRec(acc, level + "/" + item.Name , item, 1.0)
        [|
            let acc = new Queue<string * (ItemRecord * float) []>()
            getMaterialsRec(acc, item.Name, item, 1.0)
            let test = acc.ToArray()
            yield! acc.ToArray() |> Array.filter (fun (level, arr) -> arr.Length <> 0)
        |]

    member x.AddProvider(p : IRecipeProvider) = 
        providers.Add(p) |> ignore

    interface IRecipeProvider with
        member x.TryGetRecipe(item : ItemRecord) = 
            findRecipe(providers |> Seq.toList, item)