module BotDataTest.XivTest

open System

open KPX.TheBot.Data.XivData
open KPX.TheBot.Data.XivData.Recipe

open Expecto


let xivTests =
    testList "FF14测试"
    <| [ testCase "物品转换"
         <| fun _ ->
             Expect.equal (ItemCollection.Instance.GetByItemId(4).Name) "风之碎晶" ""

             let ret =
                 ItemCollection.Instance.TryGetByName("风之碎晶")

             Expect.isSome ret ""
             Expect.equal ret.Value.Name "风之碎晶" ""
             Expect.equal ret.Value.Id 4 ""

         testCase "配方测试-普通配方"
         <| fun _ ->
             let ic = ItemCollection.Instance
             let rm = XivRecipeManager.Instance
             let ret = ic.TryGetByName("亚拉戈高位合成兽革")

             Expect.isSome ret ""
             let recipe = rm.TryGetRecipe(ret.Value)
             Expect.isSome recipe ""

             let input =
                 recipe.Value.Input
                 |> Array.map (fun m -> m.Item.Name, m.Quantity)
                 |> readOnlyDict

             Expect.equal input.["合成生物的粗皮"] 3.0 ""
             Expect.equal input.["兽脂"] 9.0 ""
             Expect.equal input.["黑明矾"] 3.0 ""
             Expect.equal input.["土之晶簇"] 1.0 ""
             Expect.equal input.["风之晶簇"] 1.0 ""

         testCase "配方测试-部队工坊"
         <| fun _ ->
             let ic = ItemCollection.Instance
             let rm = XivRecipeManager.Instance

             let ret = ic.TryGetByName("奥德赛级船体")

             Expect.isSome ret ""
             let recipe = rm.TryGetRecipe(ret.Value)
             Expect.isSome recipe ""

             let input =
                 recipe.Value.Input
                 |> Array.map (fun m -> m.Item.Name, m.Quantity)
                 |> readOnlyDict

             Expect.equal input.["紫檀木材"] 24.0 ""
             Expect.equal input.["翼胶"] 9.0 ""

         testCase "海钓不应异常"
         <| fun _ ->
             for i = 0 to 72 do
                 OceanFishing.CalculateCooldown(DateTimeOffset.Now.AddHours((float i) * 2.0))
                 |> ignore

         testCase "世界名称转换"
         <| fun _ -> World.GetWorldByName("拉诺西亚") |> ignore

         ]
