module XivRecipeTest2

open KPX.TheBot.Data.XivData

open KPX.TheBot.Data.XivData.Recipe

open NUnit.Framework

[<OneTimeSetUp>]
let Setup () = InitCollection.Setup()

let ic = ItemCollection.Instance
let rm = XivRecipeManager.Instance

[<Test>]
let ``FFXIV : Recpie.CraftRecipe`` () =
    let ret = ic.TryGetByName("亚拉戈高位合成兽革")

    Assert.IsTrue(ret.IsSome)
    let recipe = rm.TryGetRecipe(ret.Value)
    Assert.IsTrue(recipe.IsSome)

    let input =
        recipe.Value.Input
        |> Array.map (fun m -> m.Item.Name, m.Quantity)
        |> readOnlyDict

    Assert.AreEqual(input.["合成生物的粗皮"], 3.0)
    Assert.AreEqual(input.["兽脂"], 9.0)
    Assert.AreEqual(input.["黑明矾"], 3.0)
    Assert.AreEqual(input.["土之晶簇"], 1.0)
    Assert.AreEqual(input.["风之晶簇"], 1.0)


[<Test>]
let ``FFXIV : Recpie.GCRecipe`` () =
    let ret = ic.TryGetByName("奥德赛级船体")

    Assert.IsTrue(ret.IsSome)
    let recipe = rm.TryGetRecipe(ret.Value)
    Assert.IsTrue(recipe.IsSome)

    let input =
        recipe.Value.Input
        |> Array.map (fun m -> m.Item.Name, m.Quantity)
        |> readOnlyDict

    Assert.AreEqual(input.["紫檀木材"], 24.0)
    Assert.AreEqual(input.["翼胶"], 9.0)
