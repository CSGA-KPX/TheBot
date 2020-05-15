module XivData.Recipe

open System
open System.Collections.Generic
open LibFFXIV.GameData.Raw
open XivData.Item

[<CLIMutable>]
type RecipeRecord =
    { [<LiteDB.BsonIdAttribute(false)>]
      Id : int
      ResultItem : ItemRecord
      ProductCount : float
      Materials : (ItemRecord * float) [] }

type FinalMaterials() =
    let m = Dictionary<ItemRecord, float>()

    member x.AddOrUpdate(item, runs) =
        if m.ContainsKey(item) then m.[item] <- m.[item] + runs
        else m.Add(item, runs)

    member x.Get() =
        [| for kv in m do
            yield (kv.Key, kv.Value) |]

type IRecipeProvider =
    abstract TryGetRecipe : ItemRecord -> RecipeRecord option


type CraftRecipeProvider private () =
    inherit Utils.XivDataSource<int, RecipeRecord>()

    static let instance = CraftRecipeProvider()
    static member Instance = instance

    override x.BuildCollection() =
        let db = x.Collection
        db.EnsureIndex("_id", true) |> ignore
        db.EnsureIndex("ResultItem.Id") |> ignore
        printfn "Building CraftRecipeProvider"
        let lookup id = Item.ItemCollection.Instance.LookupById(id)

        let chs = 
            let col = EmbeddedXivCollection(XivLanguage.ChineseSimplified, false) :> IXivCollection
            col.GetSheet("Recipe")

        let eng = 
            Utils.GlobalVerCollection.GetSheet("Recipe")

        let merged = Utils.MergeSheet(chs, eng, (fun (a,b) -> a.As<string>("Item{Result}") = "0"))

        seq {
            for row in merged do
                let itemsKeys = row.AsArray<int>("Item{Ingredient}", 10)
                let amounts = row.AsArray<byte>("Amount{Ingredient}", 10) |> Array.map (fun x -> float x)

                let materials =
                    Array.zip itemsKeys amounts
                    |> Array.filter (fun (id, _) -> id > 0)
                    |> Array.map (fun (id, runs) -> (lookup id, runs))
                yield { Id = row.Key.Main
                        ResultItem = row.As<int>("Item{Result}") |> lookup
                        ProductCount = row.As<byte>("Amount{Result}") |> float
                        Materials = materials }
        }
        |> db.InsertBulk
        |> ignore
        GC.Collect()

    interface IRecipeProvider with
        override x.TryGetRecipe(item) =
            let id = new LiteDB.BsonValue(item.Id)
            let ret = x.Collection.FindOne(LiteDB.Query.EQ("ResultItem.Id", id))
            if isNull (box ret) then
                None
            else
                Some ret

type CompanyCraftRecipeProvider private () =
    inherit Utils.XivDataSource<int, RecipeRecord>()

    static let instance = CompanyCraftRecipeProvider()
    static member Instance = instance

    override x.BuildCollection() =
        let db = x.Collection
        db.EnsureIndex("_id", true) |> ignore
        db.EnsureIndex("ResultItem.Id") |> ignore
        printfn "Building CompanyCraftRecipeProvider"
        let lookup id = Item.ItemCollection.Instance.LookupById(id)

        let chs = 
            let col = EmbeddedXivCollection(XivLanguage.ChineseSimplified, false) :> IXivCollection
            col.GetSheet("CompanyCraftSequence")

        let eng = 
            Utils.GlobalVerCollection.GetSheet("CompanyCraftSequence")

        let merged = Utils.MergeSheet(chs, eng, (fun (a,b) -> a.As<string>("ResultItem") = "0"))


        seq {
            for ccs in merged do
                let materials =
                    [| for part in ccs.AsRowArray("CompanyCraftPart", 8) do
                        for proc in part.AsRowArray("CompanyCraftProcess", 3) do
                            let itemsKeys =
                                proc.AsRowArray("SupplyItem", 12)
                                |> Array.map (fun r -> r.As<int>("Item"))

                            let amounts =
                                let setAmount = proc.AsArray<uint16>("SetQuantity", 12)
                                let setCount = proc.AsArray<uint16>("SetsRequired", 12)
                                setAmount |> Array.map2 (fun a b -> a * b |> float) setCount

                            let materials =
                                Array.zip itemsKeys amounts
                                |> Array.filter (fun (id, _) -> id > 0)
                                |> Array.map (fun (id, runs) -> (lookup id, runs))

                            yield! materials |]
                yield { Id = ccs.Key.Main
                        ResultItem = ccs.As<int>("ResultItem") |> lookup
                        ProductCount = 1.0
                        Materials = materials }
        }
        |> db.InsertBulk
        |> ignore
        GC.Collect()

    interface IRecipeProvider with
        override x.TryGetRecipe(item) =
            let id = new LiteDB.BsonValue(item.Id)
            let ret = x.Collection.FindOne(LiteDB.Query.EQ("ResultItem.Id", id))
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
            if h.IsSome then h
            else findRecipe (tail, item)



    static let instance =
        let rm = RecipeManager()
        rm.AddProvider(CraftRecipeProvider.Instance)
        rm.AddProvider(CompanyCraftRecipeProvider.Instance)
        rm

    static member GetInstance() = instance

    /// 生产一个流程的材料
    member x.GetMaterials(item : ItemRecord) =
        let recipe = (x :> IRecipeProvider).TryGetRecipe(item)
        [| if recipe.IsSome then yield! recipe.Value.Materials |]


    /// 生产一个成品的材料
    member x.GetMaterialsOne(item : ItemRecord) =
        let recipe = (x :> IRecipeProvider).TryGetRecipe(item)
        [| if recipe.IsSome then
            let recipeYield = recipe.Value.ProductCount
            let final = recipe.Value.Materials |> Array.map (fun (item, count) -> (item, count / recipeYield))
            yield! final |]


    /// 生产一个成品的基础材料
    member x.GetMaterialsRec(item : ItemRecord) =
        let rec getMaterialsRec (acc : Dictionary<ItemRecord, ItemRecord * float>, item : ItemRecord, runs : float) =
            let recipe = (x :> IRecipeProvider).TryGetRecipe(item)
            if recipe.IsNone then
                if acc.ContainsKey(item) then
                    let (item, count) = acc.[item]
                    acc.[item] <- (item, count + runs)
                else
                    acc.Add(item, (item, runs))
            else
                let realRuns = runs / recipe.Value.ProductCount
                let materials = recipe.Value.Materials |> Array.map (fun (item, count) -> (item, count * realRuns))
                for (item, count) in materials do
                    getMaterialsRec (acc, item, count)
        [| let dict = Dictionary<ItemRecord, ItemRecord * float>()
           getMaterialsRec (dict, item, 1.0)
           let ma = dict.Values |> Seq.toArray
           yield! ma |]


    /// 生产一个成品的基础材料，按物品分组
    member x.GetMaterialsRecGroup(item : ItemRecord) =
        let rec getMaterialsRec (acc : Queue<string * (ItemRecord * float) []>, level : string, item : ItemRecord,
                                 runs : float) =
            let recipe = (x :> IRecipeProvider).TryGetRecipe(item)
            if recipe.IsNone then
                ()
            else
                let realRuns = runs / recipe.Value.ProductCount
                let self = level + "*" + String.Format("{0:0.###}", 1.0), [| (item, 1.0) |]
                acc.Enqueue(self)
                let materials = recipe.Value.Materials |> Array.map (fun (item, count) -> (item, count * realRuns))
                let countStr = "*" + String.Format("{0:0.###}", 1.0)
                acc.Enqueue(level + countStr + "/", materials)
                for (item, count) in materials do
                    getMaterialsRec (acc, level + "/" + item.Name, item, 1.0)
        [| let acc = Queue<string * (ItemRecord * float) []>()
           getMaterialsRec (acc, item.Name, item, 1.0)
           let test = acc.ToArray()
           yield! acc.ToArray() |> Array.filter (fun (level, arr) -> arr.Length <> 0) |]

    member x.AddProvider(p : IRecipeProvider) = providers.Add(p) |> ignore

    interface IRecipeProvider with
        member x.TryGetRecipe(item : ItemRecord) = findRecipe (providers |> Seq.toList, item)
