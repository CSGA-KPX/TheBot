namespace KPX.TheBot.Data.XivData.Recipe

open KPX.TheBot.Data.Common.Database
open KPX.TheBot.Data.CommonModule.Recipe

open KPX.TheBot.Data.XivData


type CompanyCraftRecipeProvider private () =
    inherit CachedTableCollection<int, XivDbRecipe>(DefaultDB)

    static let instance = CompanyCraftRecipeProvider()
    static member Instance = instance

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection

        db.EnsureIndex(LiteDB.BsonExpression.Create("_id"), true)
        |> ignore

        db.EnsureIndex(LiteDB.BsonExpression.Create("Process.Output[0].Item"))
        |> ignore

        seq {
            let col = BotDataInitializer.XivCollectionChs
            let ia = ItemAccumulator<int>()

            for ccs in col.CompanyCraftSequence.TypedRows do
                ia.Clear()

                let output =
                    [| { Item = ccs.ResultItem.AsInt()
                         Quantity = 1.0 } |]

                for part in ccs.CompanyCraftPart.AsRows() do
                    for proc in part.CompanyCraftProcess.AsRows() do
                        let items =
                            proc.SupplyItem.AsRows()
                            |> Array.map (fun row -> row.Item.AsInt())

                        let amounts = proc.SetQuantity.AsDoubles()
                        let sets = proc.SetsRequired.AsDoubles()

                        for i = 0 to items.Length - 1 do
                            if items.[i] <> 0 then
                                ia.Update(items.[i], amounts.[i] * sets.[i])

                yield
                    { Id = 0
                      Process =
                          { Output = output
                            Input = ia.AsMaterials() } }

        }
        |> db.InsertBulk
        |> ignore

    interface IRecipeProvider<XivItem, RecipeProcess<XivItem>> with
        override x.TryGetRecipe(item) =
            let id = LiteDB.BsonValue(item.Id)

            let ret =
                x.DbCollection.FindOne(LiteDB.Query.EQ("Process.Output[0].Item", id))

            if isNull (box ret) then None else Some(ret.CastProcess())
