namespace KPX.XivPlugin.Data.Recipe

open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataModel.Recipe

open KPX.XivPlugin.Data


type XivRecipeManager private () =
    inherit RecipeManager<XivItem, RecipeProcess<XivItem>>()

    static let instance =
        let i = XivRecipeManager()
        i.AddProvider(CraftRecipeProvider.Instance)
        i.AddProvider(CompanyCraftRecipeProvider.Instance)
        i

    static member Instance = instance

    override x.TryGetRecipe(item) = x.SearchRecipes(item) |> Seq.tryHead

    override x.TryGetRecipe(item, quantity) =
        x.TryGetRecipe(item)
        |> Option.map
            (fun p ->
                let runs = quantity.ToRuns(p)

                { Input =
                      p.Input
                      |> Array.map (fun m -> { m with Quantity = m.Quantity * runs })
                  Output =
                      p.Output
                      |> Array.map (fun m -> { m with Quantity = m.Quantity * runs }) })

    member x.TryGetRecipeRec(material : RecipeMaterial<XivItem>) =
        x.TryGetRecipeRec(material.Item, ByItem material.Quantity)

    member x.TryGetRecipeRec(item, quantity : ProcessQuantity) =
        x.TryGetRecipe(item)
        |> Option.map
            (fun r ->
                let acc = RecipeProcessAccumulator<XivItem>()

                let rec Calc i (q : float) =
                    let recipe = x.TryGetRecipe(i, ByItem q)

                    if recipe.IsNone then
                        acc.Input.Update(i, q)
                    else
                        for m in recipe.Value.Input do
                            Calc m.Item m.Quantity

                acc.Output.Update(item, quantity.ToItems(r))
                Calc item (quantity.ToItems(r))

                acc.AsRecipeProcess())
    
    interface IDataTest with
        member x.RunTest() =
             let ic = ItemCollection.Instance
             let rm = XivRecipeManager.Instance
             let ret = ic.TryGetByName("亚拉戈高位合成兽革")

             Expect.isSome ret 
             let recipe = rm.TryGetRecipe(ret.Value)
             Expect.isSome recipe 

             let input =
                 recipe.Value.Input
                 |> Array.map (fun m -> m.Item.Name, m.Quantity)
                 |> readOnlyDict

             Expect.equal input.["合成生物的粗皮"] 3.0 
             Expect.equal input.["兽脂"] 9.0 
             Expect.equal input.["黑明矾"] 3.0 
             Expect.equal input.["土之晶簇"] 1.0 
             Expect.equal input.["风之晶簇"] 1.0
             
             
             let ret = ic.TryGetByName("奥德赛级船体")

             Expect.isSome ret 
             let recipe = rm.TryGetRecipe(ret.Value)
             Expect.isSome recipe 

             let input =
                 recipe.Value.Input
                 |> Array.map (fun m -> m.Item.Name, m.Quantity)
                 |> readOnlyDict

             Expect.equal input.["紫檀木材"] 24.0 
             Expect.equal input.["翼胶"] 9.0 
             
             