namespace BotData.XivData.Recipe

open System
open System.Collections.Generic

open BotData.CommonModule.Recipe
open BotData.XivData.Item


[<AutoOpen>]
module internal Utils =
    let inline convertInternalMaterial (i : RecipeMaterial<int32>) =
        { Item = ItemCollection.Instance.GetByKey(i.Item)
          Quantity = i.Quantity }

    let convertInternalProcess (i : RecipeProcess<int>) =
        { Input = i.Input |> Array.map convertInternalMaterial
          Output = i.Output |> Array.map convertInternalMaterial }

[<CLIMutable>]
type XivDbRecipe =
    { Id : int
      Process : RecipeProcess<int> }

    member x.CastProcess() = convertInternalProcess (x.Process)
