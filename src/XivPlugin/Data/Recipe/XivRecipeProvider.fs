namespace rec KPX.XivPlugin.Data

open KPX.TheBot.Host.DataCache

open KPX.TheBot.Host.DataModel.Recipe

open KPX.XivPlugin.Data
open KPX.XivPlugin.Data.Recipe


type XivRecipeConfig() =
    inherit RecipeConfig<XivItem, RecipeProcess<XivItem>>()

    override x.CanExpandRecipe(_) =
        // 14本身问题不大
        true

    override x.SolveSameProduct(recipes) =
        // 原则上应该使用成本来选择最优配方
        // 但Universalis本身不太稳定
        // 最后还是放弃了
        recipes |> Seq.head

type XivRecipeManager internal (providers) =
    inherit RecipeManager<XivItem, RecipeProcess<XivItem>>(providers, XivRecipeConfig())

    /// <summary>
    /// 获取国服配方集
    /// </summary>
    static member val China = XivRecipeManagerChina() :> XivRecipeManager

    /// <summary>
    /// 获取世界服配方集
    /// </summary>
    static member val Offical = XivRecipeManagerOffical() :> XivRecipeManager

    /// <summary>
    /// 根据给定的VersionRegion，获取合适的配方集
    /// </summary>
    static member GetInstance(region: VersionRegion) =
        match region with
        | VersionRegion.China -> XivRecipeManager.China
        | VersionRegion.Offical -> XivRecipeManager.Offical

    /// <summary>
    /// 根据给定的World，获取合适的配方集
    /// </summary>
    static member GetInstance(world: World) =
        match world.VersionRegion with
        | VersionRegion.China -> XivRecipeManager.China
        | VersionRegion.Offical -> XivRecipeManager.Offical

    override x.ApplyProcessQuantity(recipe, q, _) =
        recipe * (q.ToRuns(recipe, x.Config.RunRounding)) :> IRecipeProcess<_>

type private XivRecipeManagerChina() =
    inherit XivRecipeManager([ CraftRecipeProviderChina.Instance; CompanyCraftRecipeProviderChina.Instance ])

type private XivRecipeManagerOffical() =
    inherit XivRecipeManager([ CraftRecipeProviderOffical.Instance; CompanyCraftRecipeProviderOffical.Instance ])

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
            recipe.Value.Materials
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
            recipe.Value.Materials
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
            recipe.Value.Materials
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
            recipe.Value.Materials
            |> Array.map (fun m -> m.Item.OfficalName, m.Quantity)
            |> readOnlyDict

        Expect.equal input.["ローズウッド材"] 24.0
        Expect.equal input.["瞬間にかわ"] 9.0
