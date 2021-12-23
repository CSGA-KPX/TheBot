namespace KPX.XivPlugin.Data.Recipe

open KPX.TheBot.Host.DataModel.Recipe

open KPX.XivPlugin.Data


module private Utils =
    let inline convertInternalMaterial (i: RecipeMaterial<int32>, region) =
        { Item = ItemCollection.Instance.GetByItemId(i.Item, region)
          Quantity = i.Quantity }

    let inline convertInternalProcess (i: RecipeProcess<int>, region) =
        { Input = i.Input |> Array.map (fun i -> convertInternalMaterial (i, region))
          Output = i.Output |> Array.map (fun o -> convertInternalMaterial (o, region)) }

[<CLIMutable>]
type XivDbRecipe =
    { [<LiteDB.BsonIdAttribute>]
      LiteDbId: int
      Region : VersionRegion
      Process: RecipeProcess<int> }

    /// <summary>
    /// 转换XivDbRecipe到外部表示
    /// </summary>
    /// <param name="region">物品转换的版本区</param>
    member x.CastProcess(region) =
        Utils.convertInternalProcess (x.Process, region)
