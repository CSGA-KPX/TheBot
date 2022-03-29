namespace KPX.XivPlugin.Data.Recipe

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb

open KPX.TheBot.Host.DataModel.Recipe

open KPX.XivPlugin
open KPX.XivPlugin.Data
open KPX.XivPlugin.Data.Recipe


[<Sealed>]
type CraftRecipeProviderChina private () =
    inherit CachedTableCollection<XivDbRecipe>()

    static member val Instance = CraftRecipeProviderChina()

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection
        db.EnsureIndex("Process.Output[0].Item") |> ignore

        seq {
            let col = ChinaDistroData.GetCollection()

            for row in col.Recipe do
                let materials =
                    let items = row.``Item{Ingredient}``.AsInts()
                    let amounts = row.``Amount{Ingredient}``.AsDoubles()

                    Array.zip items amounts
                    |> Array.filter (fun (id, _) -> id > 0)
                    |> Array.map (fun (id, runs) -> { Item = id; Quantity = runs })

                let retItem = row.``Item{Result}``.AsInt()
                let retAmount = row.``Amount{Result}``.AsDouble()

                yield
                    { LiteDbId = 0
                      Region = VersionRegion.China
                      Process =
                          { Output = [| { Item = retItem; Quantity = retAmount } |]
                            Input = materials } }
        }
        |> db.InsertBulk
        |> ignore

    interface IRecipeProvider<XivItem, RecipeProcess<XivItem>> with
        override x.TryGetRecipe(item) =
            LiteDB.Query.EQ("Process.Output[0].Item", item.ItemId)
            |> x.DbCollection.TryFindOne
            |> Option.map (fun r -> r.CastProcess())

[<Sealed>]
type CraftRecipeProviderOffical private () =
    inherit CachedTableCollection<XivDbRecipe>()

    static member val Instance = CraftRecipeProviderOffical()

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection
        db.EnsureIndex("Process.Output[0].Item") |> ignore

        seq {
            let col = OfficalDistroData.GetCollection()

            for row in col.Recipe do
                let materials =
                    let items = row.``Item{Ingredient}``.AsInts()
                    let amounts = row.``Amount{Ingredient}``.AsDoubles()

                    Array.zip items amounts
                    |> Array.filter (fun (id, _) -> id > 0)
                    |> Array.map (fun (id, runs) -> { Item = id; Quantity = runs })

                let retItem = row.``Item{Result}``.AsInt()
                let retAmount = row.``Amount{Result}``.AsDouble()

                yield
                    { LiteDbId = 0
                      Region = VersionRegion.Offical
                      Process =
                          { Output = [| { Item = retItem; Quantity = retAmount } |]
                            Input = materials } }
        }
        |> db.InsertBulk
        |> ignore

    interface IRecipeProvider<XivItem, RecipeProcess<XivItem>> with
        override x.TryGetRecipe(item) =
            LiteDB.Query.EQ("Process.Output[0].Item", item.ItemId)
            |> x.DbCollection.TryFindOne
            |> Option.map (fun r -> r.CastProcess())