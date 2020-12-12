namespace BotData.XivData.Recipe

open BotData.CommonModule.Recipe

open BotData.XivData.Item

type XivRecipeManager private () = 
    inherit RecipeManager<ItemRecord, RecipeProcess<ItemRecord>>()

    static let instance = 
        let i = XivRecipeManager()
        i.AddProvider(CraftRecipeProvider.Instance)
        i.AddProvider(CompanyCraftRecipeProvider.Instance)
        i

    static member Instance = instance

    override x.TryGetRecipe(item) = 
        x.SearchRecipes(item)
        |> Seq.tryHead

    override x.TryGetRecipe(item, quantity) = 
        x.TryGetRecipe(item)
        |> Option.map (fun p -> 
            let runs = quantity.ToRuns(p)
            { Input = p.Input |> Array.map (fun m -> {m with Quantity = m.Quantity * runs})
              Output = p.Output |> Array.map (fun m -> {m with Quantity = m.Quantity * runs}) })

    member x.TryGetRecipeRec(material : RecipeMaterial<ItemRecord>) = 
        x.TryGetRecipeRec(material.Item, ByItem material.Quantity)

    member x.TryGetRecipeRec(item, quantity : ProcessQuantity) = 
        x.TryGetRecipe(item)
        |> Option.map (fun r ->
            let acc = RecipeProcessAccumulator<ItemRecord>()
            let rec Calc i (q : float) = 
                let recipe = x.TryGetRecipe(i, ByItem q)
                if recipe.IsNone then
                    acc.Input.Update(i, q)
                else
                    for m in recipe.Value.Input do 
                        Calc m.Item m.Quantity

            acc.Output.Update(item, quantity.ToItems(r))
            Calc item (quantity.ToItems(r))

            acc.AsRecipeProcess()
        )

