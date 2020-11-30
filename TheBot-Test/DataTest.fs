module BotDataTest

open BotData
open BotData.Common.Database
open NUnit.Framework

[<OneTimeSetUp>]
let Setup () =
    let initCollection = true
    if initCollection then
        BotDataInitializer.ClearCache()
        BotDataInitializer.ShrinkCache()
        BotDataInitializer.InitializeAllCollections()

[<Test>]
let ``FFXIV: Item.GetById`` () =
    let item = XivData.Item.ItemCollection.Instance.GetByItemId(4)
    Assert.AreEqual(item.Name, "风之碎晶")

[<Test>]
let ``FFXIV: Item.GetByName`` () =
    let ret = XivData.Item.ItemCollection.Instance.TryGetByName("风之碎晶")
    Assert.IsTrue(ret.IsSome)
    Assert.AreEqual(ret.Value.Name, "风之碎晶")
    Assert.AreEqual(ret.Value.Id, 4)

[<Test>]
let ``FFXIV : Recpie.CraftRecipe`` () = 
    let rm = XivData.Recipe.RecipeManager.GetInstance()
    let ret = XivData.Item.ItemCollection.Instance.TryGetByName("亚拉戈高位合成兽革")
    Assert.IsTrue(ret.IsSome)
    let item = ret.Value
    let recipe =
        rm.GetMaterials(item)
        |> Array.map (fun m -> m.Item.Name, m.Quantity)
        |> readOnlyDict

    Assert.AreEqual(recipe.["合成生物的粗皮"], 3)
    Assert.AreEqual(recipe.["兽脂"], 9)
    Assert.AreEqual(recipe.["黑明矾"], 3)
    Assert.AreEqual(recipe.["土之晶簇"], 1)
    Assert.AreEqual(recipe.["风之晶簇"], 1)


[<Test>]
let ``FFXIV : Recpie.GCRecipe`` () = 
    let rm = XivData.Recipe.RecipeManager.GetInstance()
    let ret = XivData.Item.ItemCollection.Instance.TryGetByName("奥德赛级船体")
    Assert.IsTrue(ret.IsSome)
    let item = ret.Value
    let recipe =
        rm.GetMaterials(item)
        |> Array.map (fun m -> m.Item.Name, m.Quantity)
        |> readOnlyDict
    Assert.AreEqual(recipe.["紫檀木材"], 24)
    Assert.AreEqual(recipe.["翼胶"], 9)

[<Test>]
let ``EVE : Type.getById`` () = 
    let tc = EveData.EveType.EveTypeCollection.Instance
    let item = tc.GetById(34)
    Assert.AreEqual(item.Name, "三钛合金")

[<Test>]
let ``EVE : Type.getByName`` () = 
    let tc = EveData.EveType.EveTypeCollection.Instance
    let ret = tc.TryGetByName("三钛合金")
    Assert.IsTrue(ret.IsSome)
    Assert.AreEqual(ret.Value.Id, 34)
    Assert.AreEqual(ret.Value.Name, "三钛合金")