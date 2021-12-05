namespace KPX.XivPlugin.Data.Recipe

open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataModel.Recipe

open KPX.XivPlugin.Data


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

        let col = XivProvider.XivCollectionChs

        seq {
            for row in col.Recipe.TypedRows do
                let materials =
                    let items = row.``Item{Ingredient}``.AsInts()
                    let amounts = row.``Amount{Ingredient}``.AsDoubles()

                    Array.zip items amounts
                    |> Array.filter (fun (id, _) -> id > 0)
                    |> Array.map (fun (id, runs) -> { Item = id; Quantity = runs })

                let retItem = row.``Item{Result}``.AsInt()
                let retAmount = row.``Amount{Result}``.AsDouble()

                yield
                    { Id = 0
                      Process =
                          { Output = [| { Item = retItem; Quantity = retAmount } |]
                            Input = materials } }
        }
        |> db.InsertBulk
        |> ignore

    interface IRecipeProvider<XivItem, RecipeProcess<XivItem>> with
        override x.TryGetRecipe(item) =
            let id = LiteDB.BsonValue(item.Id)

            let ret = x.DbCollection.FindOne(LiteDB.Query.EQ("Process.Output[0].Item", id))

            if isNull (box ret) then
                None
            else
                Some(ret.CastProcess())
