namespace BotData.XivData.Recipe

open BotData.Common.Database
open BotData.CommonModule.Recipe

open BotData.XivData.Item

type CraftRecipeProvider private () =
    inherit CachedTableCollection<int, XivDbRecipe>()

    static let instance = CraftRecipeProvider()
    static member Instance = instance

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() = 
        let db = x.DbCollection
        db.EnsureIndex(LiteDB.BsonExpression.Create("_id"), true) |> ignore
        db.EnsureIndex(LiteDB.BsonExpression.Create("Process.Output[0].Item")) |> ignore

        let chs = BotDataInitializer.GetXivCollectionChs().GetSheet("Recipe")

        seq {
            for row in chs do
                let itemsKeys = row.AsArray<int>("Item{Ingredient}", 10)
                let amounts = row.AsArray<byte>("Amount{Ingredient}", 10) |> Array.map (fun x -> float x)
                let materials =
                    Array.zip itemsKeys amounts
                    |> Array.filter (fun (id, _) -> id > 0)
                    |> Array.map (fun (id, runs) -> {Item = id; Quantity = runs})
                let resultItem = row.As<int>("Item{Result}")
                let resultAmount = row.As<byte>("Amount{Result}")
                yield { Id = 0
                        Process = { Output = [|{ Item = resultItem; 
                                                Quantity = resultAmount |> float }|]
                                    Input = materials } }
        }
        |> db.InsertBulk
        |> ignore

    interface IRecipeProvider<ItemRecord, RecipeProcess<ItemRecord>> with
        override x.TryGetRecipe(item) =
            let id = new LiteDB.BsonValue(item.Id)
            let ret = x.DbCollection.FindOne(LiteDB.Query.EQ("Process.Output[0].Item", id))
            if isNull (box ret) then
                None
            else
                Some (ret.CastProcess())