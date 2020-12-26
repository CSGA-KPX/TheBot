namespace KPX.TheBot.Data.CommonModule.Recipe

open System.Collections.Generic


[<Struct>]
type ProcessQuantity =
    | ByRun of runs : float
    | ByItem of items : float

    member x.ToItems(proc : RecipeProcess<_>) =
        match x with
        | ByItem value -> value
        | ByRun value ->
            let qPerRun = proc.GetFirstProduct().Quantity
            value * qPerRun

    /// 将物品数转换为流程数
    member x.ToRuns(proc : RecipeProcess<_>) =
        match x with
        | ByRun value -> value
        | ByItem value ->
            let qPerRun = proc.GetFirstProduct().Quantity
            value / qPerRun

[<AbstractClass>]
type RecipeManager<'Item, 'Recipe when 'Item : equality>() =
    let providers = List<IRecipeProvider<'Item, 'Recipe>>()

    member x.AddProvider(p) = providers.Add(p)

    /// 从Provider查找所有配方
    member internal x.SearchRecipes(item : 'Item) =
        providers
        |> Seq.choose (fun p -> p.TryGetRecipe(item))

    /// 查找指定物品的生产配方
    abstract TryGetRecipe : 'Item -> 'Recipe option
    abstract TryGetRecipe : 'Item * ProcessQuantity -> 'Recipe option

    member x.TryGetRecipe(material : RecipeMaterial<'Item>) =
        x.TryGetRecipe(material.Item, ByItem material.Quantity)

    member x.GetRecipe(item : 'Item) = x.TryGetRecipe(item).Value

    member x.GetRecipe(item : 'Item, quantity : ProcessQuantity) =
        x.TryGetRecipe(item, quantity).Value

    member x.GetRecipe(material : RecipeMaterial<'Item>) = x.TryGetRecipe(material).Value
