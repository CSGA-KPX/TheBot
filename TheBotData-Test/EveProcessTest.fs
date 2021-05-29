module EveProcessTest

open KPX.TheBot.Data.CommonModule.Recipe

open KPX.TheBot.Data.EveData.EveType
open KPX.TheBot.Data.EveData.Process
open NUnit.Framework

[<OneTimeSetUp>]
let Setup () = InitCollection.Setup()

let ec = EveTypeCollection.Instance

let rm =
    EveProcessManager(
        { new IEveCalculatorConfig with
            member x.InputMe = 3
            member x.DerivationMe = 10
            member x.ExpandPlanet = false
            member x.ExpandReaction = false }
    )

let process2Dict (proc : RecipeProcess<EveType>) =
    seq {
        for i in proc.Input do
            yield i.Item.Name, i.Quantity |> int

        for i in proc.Output do
            yield i.Item.Name, i.Quantity |> int
    }
    |> readOnlyDict


[<Test>]
let ``EVE : recipe from bp me:2`` () =
    // TODO 换成T1战巡
    let ret =
        rm.TryGetRecipe(ec.TryGetByName("麦基诺级蓝图").Value, ByItem 1.0)

    Assert.IsTrue(ret.IsSome)
    let d = process2Dict (ret.Value.ApplyFlags(ProcessFlag.MeApplied))
    Assert.AreEqual(d.["建筑模块"], 146)
    Assert.AreEqual(d.["莫尔石"], 107)
    Assert.AreEqual(d.["R.A.M. - 星舰科技"], 15)
    Assert.AreEqual(d.["离子推进器"], 59)
    Assert.AreEqual(d.["磁力感应器组"], 219)
    Assert.AreEqual(d.["光子微处理器"], 2910)
    Assert.AreEqual(d.["碳化晶体附甲"], 2910)
    Assert.AreEqual(d.["聚变反应堆机组"], 44)
    Assert.AreEqual(d.["震荡电容器单元"], 582)
    Assert.AreEqual(d.["脉冲护盾发射器"], 219)
    Assert.AreEqual(d.["回旋者级"], 1)

[<Test>]
let ``EVE : recipe from item me:2`` () =
    // TODO 换成T1战巡
    let ret =
        rm.TryGetRecipe(ec.TryGetByName("麦基诺级").Value, ByItem 1.0)

    Assert.IsTrue(ret.IsSome)
    let d = process2Dict (ret.Value.ApplyFlags(ProcessFlag.MeApplied))
    Assert.AreEqual(d.["建筑模块"], 146)
    Assert.AreEqual(d.["莫尔石"], 107)
    Assert.AreEqual(d.["R.A.M. - 星舰科技"], 15)
    Assert.AreEqual(d.["离子推进器"], 59)
    Assert.AreEqual(d.["磁力感应器组"], 219)
    Assert.AreEqual(d.["光子微处理器"], 2910)
    Assert.AreEqual(d.["碳化晶体附甲"], 2910)
    Assert.AreEqual(d.["聚变反应堆机组"], 44)
    Assert.AreEqual(d.["震荡电容器单元"], 582)
    Assert.AreEqual(d.["脉冲护盾发射器"], 219)
    Assert.AreEqual(d.["回旋者级"], 1)

[<Test>]
let ``EVE : recipe rec from item me:2_Control`` () =
    // TODO 换成T2战巡
    let ret =
        rm.TryGetRecipeRecMe(ec.TryGetByName("麦基诺级").Value, ByItem 1.0)

    Assert.IsTrue(ret.IsSome)
    let d = process2Dict (ret.Value.FinalProcess)
    Assert.AreEqual(d.["建筑模块"], 146)
    Assert.AreEqual(d.["莫尔石"], 107)
    Assert.AreEqual(d.["三钛合金"], 1440075)
    Assert.AreEqual(d.["类晶体胶矿"], 270060)
    Assert.AreEqual(d.["类银超金属"], 67530)
    Assert.AreEqual(d.["同位聚合体"], 36011)
    Assert.AreEqual(d.["超新星诺克石"], 13504)
    Assert.AreEqual(d.["酚合成物"], 17637)
    Assert.AreEqual(d.["铁磁胶体"], 278)
    Assert.AreEqual(d.["碳化晶体"], 187374)
    Assert.AreEqual(d.["纳米晶体管"], 6621)
    Assert.AreEqual(d.["超级突触纤维"], 438)
    Assert.AreEqual(d.["光子超材料"], 6984)
    Assert.AreEqual(d.["多晶碳化硅纤维"], 31071)
    Assert.AreEqual(d.["费米子冷凝物"], 88)
    Assert.AreEqual(d.["富勒化合物"], 5820)
    Assert.AreEqual(d.["晶状石英核岩"], 2250)
    Assert.AreEqual(d.["超噬矿"], 1260)

[<Test>]
let ``EVE : recipe rec from item me:2`` () =
    // TODO 换成T2战巡
    let ret =
        rm.TryGetRecipeRecMe(ec.TryGetByName("麦基诺级").Value, ByItem 1.0)

    Assert.IsTrue(ret.IsSome)

    let d =
        process2Dict (ret.Value.FinalProcess)

    for kv in d do
        System.Console.WriteLine("{0} : {1}", kv.Key, kv.Value)

    Assert.AreEqual(d.["建筑模块"], 146)
    Assert.AreEqual(d.["莫尔石"], 107)
    Assert.AreEqual(d.["三钛合金"], 1440075)
    Assert.AreEqual(d.["类晶体胶矿"], 270060)
    Assert.AreEqual(d.["类银超金属"], 67530)
    Assert.AreEqual(d.["同位聚合体"], 36011)
    Assert.AreEqual(d.["超新星诺克石"], 13504)
    Assert.AreEqual(d.["酚合成物"], 17637)
    Assert.AreEqual(d.["铁磁胶体"], 278)
    Assert.AreEqual(d.["碳化晶体"], 187374)
    Assert.AreEqual(d.["纳米晶体管"], 6621)
    Assert.AreEqual(d.["超级突触纤维"], 438)
    Assert.AreEqual(d.["光子超材料"], 6984)
    Assert.AreEqual(d.["多晶碳化硅纤维"], 31071)
    Assert.AreEqual(d.["费米子冷凝物"], 88)
    Assert.AreEqual(d.["富勒化合物"], 5820)
    Assert.AreEqual(d.["晶状石英核岩"], 2250)
    Assert.AreEqual(d.["超噬矿"], 1260)
    System.Console.WriteLine("Final : {0}", sprintf "%A" ret.Value.FinalProcess)
    System.Console.WriteLine("Intermediate : {0}", sprintf "%A" ret.Value.IntermediateProcess)

[<Test>]
let ``Refine Ore`` () =
    let ore = ec.TryGetByName("凡晶石").Value

    let refine =
        RefineProcessCollection.Instance.GetProcessFor(ore)

    let d = process2Dict (refine.ApplyFlags(ProcessFlag.MeApplied))
    System.Console.Write(sprintf "%A" refine)
    Assert.AreEqual(d.["三钛合金"], 400)

[<Test>]
let ``Refine Ice`` () =
    let ore = ec.TryGetByName("冰晶矿").Value

    let refine =
        RefineProcessCollection.Instance.GetProcessFor(ore)

    System.Console.Write(sprintf "%A" refine)
    let d = process2Dict (refine.ApplyFlags(ProcessFlag.MeApplied))
    Assert.AreEqual(d.["重水"], 173)
    Assert.AreEqual(d.["液化臭氧"], 691)
    Assert.AreEqual(d.["锶包合物"], 173)
