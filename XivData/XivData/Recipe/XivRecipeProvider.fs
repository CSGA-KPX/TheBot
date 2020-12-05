namespace BotData.XivData.Recipe

open BotData.CommonModule.Recipe
open BotData.XivData.Item

type XivRecipeManager private () = 
    inherit RecipeManager<ItemRecord>()

    static let instance = 
        let i = XivRecipeManager()
        i.AddProvider(CraftRecipeProvider.Instance)
        i.AddProvider(CompanyCraftRecipeProvider.Instance)
        i

    static member Instance = instance

    override x.TryGetRecipe(item, quantity) = 
        x.TryGetRecipe(item)
        |> Option.map (fun m -> m * quantity)