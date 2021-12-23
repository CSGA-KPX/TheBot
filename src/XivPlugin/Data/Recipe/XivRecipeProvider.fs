namespace KPX.XivPlugin.Data

open KPX.TheBot.Host.DataCache

open KPX.TheBot.Host.DataModel.Recipe

open KPX.XivPlugin.Data
open KPX.XivPlugin.Data.Recipe


[<Sealed>]
type XivRecipeManager private () =
    inherit RecipeManager<XivItem, RecipeProcess<XivItem>>()

    /// <summary>
    /// 获取国服配方集
    /// </summary>
    static member val China =
        let i = XivRecipeManager()
        i.AddProvider(CraftRecipeProviderChina.Instance)
        i.AddProvider(CompanyCraftRecipeProviderChina.Instance)
        i

    /// <summary>
    /// 获取世界服配方集
    /// </summary>
    static member val Offical =
        let i = XivRecipeManager()
        i.AddProvider(CraftRecipeProviderOffical.Instance)
        i.AddProvider(CompanyCraftRecipeProviderOffical.Instance)
        i

    /// <summary>
    /// 根据给定的VersionRegion，获取合适的配方集
    /// </summary>
    static member GetInstance(region : VersionRegion) =
        match region with
        | VersionRegion.China -> XivRecipeManager.China
        | VersionRegion.Offical -> XivRecipeManager.Offical

    /// <summary>
    /// 根据给定的World，获取合适的配方集
    /// </summary>
    static member GetInstance(world : World) =
        match world.VersionRegion with
        | VersionRegion.China -> XivRecipeManager.China
        | VersionRegion.Offical -> XivRecipeManager.Offical

    /// <summary>
    /// 获取指定道具的1流程的制作配方 ProcessQuantity.ByRun
    ///
    /// 如果该道具不存在制作配方，返回None
    /// </summary>
    /// <param name="item">查找的道具</param>
    override x.TryGetRecipe(item) = x.SearchRecipes(item) |> Seq.tryHead

    /// <summary>
    /// 获取指定道具的指定数量的制作配方
    ///
    /// 如果该道具不存在制作配方，返回None
    /// </summary>
    /// <param name="item">查找的道具</param>
    /// <param name="quantity">制作数量 ProcessQuantity.ByItem</param>
    override x.TryGetRecipe(item, quantity) =
        x.TryGetRecipe(item)
        |> Option.map
            (fun p ->
                let runs = quantity.ToRuns(p)

                { Input = p.Input |> Array.map (fun m -> { m with Quantity = m.Quantity * runs })
                  Output = p.Output |> Array.map (fun m -> { m with Quantity = m.Quantity * runs }) })

    /// <summary>
    /// 递归获取指定道具的指定数量的制作配方
    ///
    /// 如果该道具不存在制作配方，返回None
    /// </summary>
    /// <param name="material">物品和数量</param>
    member x.TryGetRecipeRec(material: RecipeMaterial<XivItem>) =
        x.TryGetRecipeRec(material.Item, ByItem material.Quantity)

    /// <summary>
    /// 递归获取指定道具的指定数量的制作配方
    ///
    /// 如果该道具不存在制作配方，返回None
    /// </summary>
    /// <param name="item">查找的道具</param>
    /// <param name="quantity">制作数量 ProcessQuantity.ByItem</param>
    member x.TryGetRecipeRec(item, quantity: ProcessQuantity) =
        x.TryGetRecipe(item)
        |> Option.map
            (fun r ->
                let acc = RecipeProcessAccumulator<XivItem>()

                let rec Calc i (q: float) =
                    let recipe = x.TryGetRecipe(i, ByItem q)

                    if recipe.IsNone then
                        acc.Input.Update(i, q)
                    else
                        for m in recipe.Value.Input do
                            Calc m.Item m.Quantity

                acc.Output.Update(item, quantity.ToItems(r))
                Calc item (quantity.ToItems(r))

                acc.AsRecipeProcess())

[<Sealed>]
type ChinaRecipeTest() =
    inherit DataTest()

    override x.RunTest() =
        let ic = ItemCollection.Instance
        let rm = XivRecipeManager.China

        let ret = ic.TryGetByName("亚拉戈高位合成兽革")

        Expect.isSome ret
        let recipe = rm.TryGetRecipe(ret.Value)
        Expect.isSome recipe

        let input =
            recipe.Value.Input
            |> Array.map (fun m -> m.Item.ChineseName, m.Quantity)
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
            |> Array.map (fun m -> m.Item.ChineseName, m.Quantity)
            |> readOnlyDict

        Expect.equal input.["紫檀木材"] 24.0
        Expect.equal input.["翼胶"] 9.0

[<Sealed>]
type OfficalRecipeTest() =
    inherit DataTest()

    override x.RunTest() =
        let ic = ItemCollection.Instance
        let rm = XivRecipeManager.China

        let ret = ic.TryGetByName("ハイアラガンキメラレザー")

        Expect.isSome ret
        let recipe = rm.TryGetRecipe(ret.Value)
        Expect.isSome recipe

        let input =
            recipe.Value.Input
            |> Array.map (fun m -> m.Item.OfficalName, m.Quantity)
            |> readOnlyDict

        Expect.equal input.["強化キメラ生物の粗皮"] 3.0
        Expect.equal input.["獣脂"] 9.0
        Expect.equal input.["ブラックアルメン"] 3.0
        Expect.equal input.["アースクラスター"] 1.0
        Expect.equal input.["ウィンドクラスター"] 1.0


        let ret = ic.TryGetByName("オデッセイ級船体")

        Expect.isSome ret
        let recipe = rm.TryGetRecipe(ret.Value)
        Expect.isSome recipe

        let input =
            recipe.Value.Input
            |> Array.map (fun m -> m.Item.OfficalName, m.Quantity)
            |> readOnlyDict

        Expect.equal input.["ローズウッド材"] 24.0
        Expect.equal input.["瞬間にかわ"] 9.0
