namespace KPX.XivPlugin.Data.Recipe

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb

open KPX.TheBot.Host.DataModel.Recipe

open KPX.XivPlugin
open KPX.XivPlugin.Data
open KPX.XivPlugin.Data.Recipe


[<Sealed>]
type CompanyCraftRecipeProviderChina private () =
    inherit CachedTableCollection<XivDbRecipe>()

    static member val Instance = CompanyCraftRecipeProviderChina()

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection

        db.EnsureIndex(LiteDB.BsonExpression.Create("_id"), true) |> ignore

        db.EnsureIndex(LiteDB.BsonExpression.Create("Process.Output[0].Item")) |> ignore

        seq {
            let col = ChinaDistroData.GetCollection()
            let ia = ItemAccumulator<int>()

            for ccs in col.CompanyCraftSequence.TypedRows do
                ia.Clear()

                let output =
                    [| { Item = ccs.ResultItem.AsInt()
                         Quantity = 1.0 } |]

                for part in ccs.CompanyCraftPart.AsRows() do
                    for proc in part.CompanyCraftProcess.AsRows() do
                        let items = proc.SupplyItem.AsRows() |> Array.map (fun row -> row.Item.AsInt())

                        let amounts = proc.SetQuantity.AsDoubles()
                        let sets = proc.SetsRequired.AsDoubles()

                        for i = 0 to items.Length - 1 do
                            if items.[i] <> 0 then
                                ia.Update(items.[i], amounts.[i] * sets.[i])

                yield
                    { LiteDbId = 0
                      Region = VersionRegion.China
                      Process =
                          { Output = output
                            Input = ia.AsMaterials() } }

        }
        |> db.InsertBulk
        |> ignore

    interface IRecipeProvider<XivItem, RecipeProcess<XivItem>> with
        override x.TryGetRecipe(item) =
            LiteDB.Query.EQ("Process.Output[0].Item", item.ItemId)
            |> x.DbCollection.TryFindOne
            |> Option.map (fun r -> r.CastProcess())

[<Sealed>]
type CompanyCraftRecipeProviderOffical private () =
    inherit CachedTableCollection<XivDbRecipe>()

    static member val Instance = CompanyCraftRecipeProviderOffical()

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection

        db.EnsureIndex(LiteDB.BsonExpression.Create("_id"), true) |> ignore

        db.EnsureIndex(LiteDB.BsonExpression.Create("Process.Output[0].Item")) |> ignore

        seq {
            let col = OfficalDistroData.GetCollection()
            let ia = ItemAccumulator<int>()

            for ccs in col.CompanyCraftSequence.TypedRows do
                ia.Clear()

                let output =
                    [| { Item = ccs.ResultItem.AsInt()
                         Quantity = 1.0 } |]

                for part in ccs.CompanyCraftPart.AsRows() do
                    for proc in part.CompanyCraftProcess.AsRows() do
                        let items = proc.SupplyItem.AsRows() |> Array.map (fun row -> row.Item.AsInt())

                        let amounts = proc.SetQuantity.AsDoubles()
                        let sets = proc.SetsRequired.AsDoubles()

                        for i = 0 to items.Length - 1 do
                            if items.[i] <> 0 then
                                ia.Update(items.[i], amounts.[i] * sets.[i])

                yield
                    { LiteDbId = 0
                      Region = VersionRegion.China
                      Process =
                          { Output = output
                            Input = ia.AsMaterials() } }

        }
        |> db.InsertBulk
        |> ignore

    interface IRecipeProvider<XivItem, RecipeProcess<XivItem>> with
        override x.TryGetRecipe(item) =
            LiteDB.Query.EQ("Process.Output[0].Item", item.ItemId)
            |> x.DbCollection.TryFindOne
            |> Option.map (fun r -> r.CastProcess())