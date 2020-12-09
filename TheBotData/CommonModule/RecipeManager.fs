namespace BotData.CommonModule.Recipe

open System.Collections.Generic

[<AbstractClass>]
type RecipeManager<'Item when 'Item : equality>() = 
    let providers = List<IRecipeProvider<'Item>>()


    member x.AddProvider(p) = providers.Add(p)

    /// 查找指定物品的所有生产配方
    member x.GetRecipes(item : 'Item) =
        providers
        |> Seq.choose (fun p -> p.TryGetRecipe(item))

    /// 查找指定物品的生产配方
    member x.TryGetRecipe(item : 'Item)  = 
        x.GetRecipes(item) |> Seq.tryHead
    /// 查找指定数量物品的配方
    ///
    /// 因为FF14和EVE对材料计算差别，由子类实现
    abstract TryGetRecipe : 'Item * float -> RecipeProcess<'Item> option
    member x.TryGetRecipe(material : RecipeMaterial<'Item>) = 
        x.TryGetRecipe(material.Item, material.Quantity)

    member x.GetRecipe(item : 'Item) = x.TryGetRecipe(item).Value
    member x.GetRecipe(item : 'Item, quantity) = x.TryGetRecipe(item, quantity).Value
    member x.GetRecipe(material : RecipeMaterial<'Item>) = x.TryGetRecipe(material).Value

    member x.GetRecipeRec(output : RecipeMaterial<'Item>) = 
        let acc = RecipeProcessAccumulator<'Item>()

        let rec Calc (m : RecipeMaterial<'Item>) = 
            let recipe = x.TryGetRecipe(m)
            if recipe.IsNone then
                acc.Input.Update(m)
            else
                for m in recipe.Value.Input do 
                    Calc m

        acc.Output.Update(output)
        Calc output

        acc.AsRecipeProcess()

    member x.GetRecipeRec(item, quantity) = 
        x.GetRecipeRec({Item = item; Quantity = quantity})

    member x.TryGetRecipeRec(output : RecipeMaterial<'Item>) = 
        let ret = x.GetRecipeRec(output)
        if ret.Input.Length = 0 then None else Some ret

    member x.TryGetRecipeRec(item, quantity) = 
        x.TryGetRecipeRec({Item = item; Quantity = quantity})