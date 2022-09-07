namespace KPX.XivPlugin.Data.Recipe

open KPX.TheBot.Host.DataModel.Recipe

open KPX.XivPlugin.Data


module private Utils =
    let inline convertInternalMaterial (i: RecipeMaterial<int32>) =
        { Item = ItemCollection.Instance.GetByItemId(i.Item)
          Quantity = i.Quantity }

    let inline convertInternalProcess (i: RecipeProcess<int>) =
        { Materials = i.Materials |> Array.map (fun i -> convertInternalMaterial (i))
          Product = i.Product |> convertInternalMaterial }

[<CLIMutable>]
type XivDbRecipe =
    { [<LiteDB.BsonIdAttribute>]
      LiteDbId: int
      Region: VersionRegion
      Process: RecipeProcess<int> }

    /// <summary>
    /// 转换XivDbRecipe到外部表示
    /// </summary>
    /// <param name="region">物品转换的版本区</param>
    member x.CastProcess() = Utils.convertInternalProcess x.Process
