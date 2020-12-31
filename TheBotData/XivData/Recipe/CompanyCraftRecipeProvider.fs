namespace KPX.TheBot.Data.XivData.Recipe

open KPX.TheBot.Data.Common.Database
open KPX.TheBot.Data.CommonModule.Recipe

open KPX.TheBot.Data.XivData


type CompanyCraftRecipeProvider private () =
    inherit CachedTableCollection<int, XivDbRecipe>()

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

        use col = BotDataInitializer.XivCollectionChs
        let chs = col.GetSheet("CompanyCraftSequence")

        seq {
            for ccs in chs do
                let materials =
                    [| for part in ccs.AsRowArray("CompanyCraftPart", 8) do
                        for proc in part.AsRowArray("CompanyCraftProcess", 3) do
                            let itemsKeys =
                                proc.AsRowArray("SupplyItem", 12)
                                |> Array.map (fun r -> r.As<int>("Item"))

                            let amounts =
                                let setAmount = proc.AsArray<uint16>("SetQuantity", 12)
                                let setCount = proc.AsArray<uint16>("SetsRequired", 12)

                                setAmount
                                |> Array.map2 (fun a b -> a * b |> float) setCount

                            let materials =
                                Array.zip itemsKeys amounts
                                |> Array.filter (fun (id, _) -> id > 0)
                                |> Array.map (fun (id, runs) -> { Item = id; Quantity = runs })

                            yield! materials |]

                yield
                    { Id = 0
                      Process =
                          { Output =
                                [| { Item = ccs.As<int>("ResultItem")
                                     Quantity = 1.0 } |]
                            Input = materials } }
        }
        |> db.InsertBulk
        |> ignore

    interface IRecipeProvider<XivItem, RecipeProcess<XivItem>> with
        override x.TryGetRecipe(item) =
            let id = new LiteDB.BsonValue(item.Id)

            let ret =
                x.DbCollection.FindOne(LiteDB.Query.EQ("Process.Output[0].Item", id))

            if isNull (box ret) then None else Some(ret.CastProcess())
