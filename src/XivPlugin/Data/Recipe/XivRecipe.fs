namespace KPX.XivPlugin.Data.Recipe

open KPX.TheBot.Host.DataModel.Recipe

open KPX.XivPlugin.Data


[<AutoOpen>]
module internal Utils =
    let inline convertInternalMaterial (i : RecipeMaterial<int32>) =
        { Item = ItemCollection.Instance.GetByItemId(i.Item)
          Quantity = i.Quantity }

    let convertInternalProcess (i : RecipeProcess<int>) =
        { Input = i.Input |> Array.map convertInternalMaterial
          Output = i.Output |> Array.map convertInternalMaterial }

[<CLIMutable>]
type XivDbRecipe =
    { Id : int
      Process : RecipeProcess<int> }

    member x.CastProcess() = convertInternalProcess x.Process
