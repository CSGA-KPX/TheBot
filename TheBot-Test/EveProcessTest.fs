module EveProcessTest

open BotData.CommonModule.Recipe

open BotData.EveData.EveType
open BotData.EveData.Process
open NUnit.Framework

[<OneTimeSetUp>]
let Setup () = InitCollection.Setup()

let ec = EveTypeCollection.Instance
let rm = EveProcessManager({new IEveCalculatorConfig with
                                member x.InputME = 2
                                member x.DerivedME = 10
                                member x.ExpandPlanet = false
                                member x.ExpandReaction = false})

let process2Dict (proc : RecipeProcess<EveType>) = 
    seq {
        for i in proc.Input do
            yield i.Item.Name, i.Quantity |> ceil |> int
        for i in proc.Output do
            yield i.Item.Name, i.Quantity |> ceil |> int
    }
    |> readOnlyDict
    

[<Test>]
let ``EVE : recipe from bp me:2`` () = 
    let ret = rm.TryGetRecipeMe(ec.TryGetByName("噩梦级蓝图").Value, ByItem 1.0)
    Assert.IsTrue(ret.IsSome)
    let d = process2Dict(ret.Value.Process)
    Assert.AreEqual(d.["三钛合金"], 11737333)
    Assert.AreEqual(d.["类晶体胶矿"], 2768251)
    Assert.AreEqual(d.["类银超金属"], 707647)
    Assert.AreEqual(d.["同位聚合体"], 176854)
    Assert.AreEqual(d.["超新星诺克石"], 44403)
    Assert.AreEqual(d.["晶状石英核岩"], 19640)
    Assert.AreEqual(d.["超噬矿"], 7105)

[<Test>]
let ``EVE : recipe from item me:2`` () = 
    let ret = rm.TryGetRecipeMe(ec.TryGetByName("噩梦级").Value, ByItem 1.0)
    Assert.IsTrue(ret.IsSome)
    let d = process2Dict(ret.Value.Process)
    Assert.AreEqual(d.["三钛合金"], 11737333)
    Assert.AreEqual(d.["类晶体胶矿"], 2768251)
    Assert.AreEqual(d.["类银超金属"], 707647)
    Assert.AreEqual(d.["同位聚合体"], 176854)
    Assert.AreEqual(d.["超新星诺克石"], 44403)
    Assert.AreEqual(d.["晶状石英核岩"], 19640)
    Assert.AreEqual(d.["超噬矿"], 7105)

[<Test>]
let ``EVE : recipe rec from item me:2_Control`` () = 
    let ret = rm.TryGetRecipeMe(ec.TryGetByName("恶狼级").Value, ByItem 1.0)
    Assert.IsTrue(ret.IsSome)
    let d = process2Dict(ret.Value.Process)
    Assert.AreEqual(d.["等离子推进器"], 368)
    Assert.AreEqual(d.["莫尔石"], 956)
    Assert.AreEqual(d.["碳化菲尔金属合成物附甲"], 36750)
    Assert.AreEqual(d.["光雷达感应器组"], 846)
    Assert.AreEqual(d.["纳米机械微处理器"], 5880)
    Assert.AreEqual(d.["偏阻护盾发射器"], 3720)
    Assert.AreEqual(d.["建筑模块"], 441)
    Assert.AreEqual(d.["R.A.M. - 星舰科技"], 30)
    Assert.AreEqual(d.["电解电容器单元"], 2940)
    Assert.AreEqual(d.["核反应堆机组"], 221)
    Assert.AreEqual(d.["狂暴级"], 1)

[<Test>]
let ``EVE : recipe rec from item me:2`` () = 
    let ret = rm.TryGetRecipeRecMe(ec.TryGetByName("恶狼级").Value, ByItem 1.0)
    Assert.IsTrue(ret.IsSome)
    let d = process2Dict(ret.Value.FinalProcess.Process)
    for kv in d do System.Console.WriteLine("{0} : {1}", kv.Key, kv.Value)
    Assert.AreEqual(d.["酚合成物"], 32746)
    Assert.AreEqual(d.["菲尔合金碳化物"], 1713210)
    Assert.AreEqual(d.["铁磁胶体"], 3680)
    Assert.AreEqual(d.["莫尔石"], 956)
    Assert.AreEqual(d.["多晶碳化硅纤维"], 393957)
    Assert.AreEqual(d.["超级突触纤维"], 1523)
    Assert.AreEqual(d.["纳米晶体管"], 13992)
    Assert.AreEqual(d.["等离子体超材料"], 15876)
    Assert.AreEqual(d.["建筑模块"], 441)
    Assert.AreEqual(d.["三钛合金"], 9289852)
    Assert.AreEqual(d.["类晶体胶矿"], 2323420)
    Assert.AreEqual(d.["类银超金属"], 582961)
    Assert.AreEqual(d.["同位聚合体"], 145383)
    Assert.AreEqual(d.["超新星诺克石"], 36261)
    Assert.AreEqual(d.["富勒化合物"], 29106)
    Assert.AreEqual(d.["费米子冷凝物"], 398)
    Assert.AreEqual(d.["晶状石英核岩"], 17100)
    Assert.AreEqual(d.["超噬矿"], 4920)
    System.Console.WriteLine("Final : {0}", sprintf "%A" ret.Value.FinalProcess)
    System.Console.WriteLine("Intermediate : {0}", sprintf "%A" ret.Value.IntermediateProcess)

[<Test>]
let ``Refine Ore`` () = 
    let ore = ec.TryGetByName("凡晶石").Value
    let refine = RefineProcessCollection.Instance.GetProcessFor(ore)
    let d = process2Dict(refine.Process)
    System.Console.Write(sprintf "%A" refine)
    Assert.AreEqual(d.["三钛合金"], 400)

[<Test>]
let ``Refine Ice`` () = 
    let ore = ec.TryGetByName("冰晶矿").Value
    let refine = RefineProcessCollection.Instance.GetProcessFor(ore)
    System.Console.Write(sprintf "%A" refine)
    let d = process2Dict(refine.Process)
    Assert.AreEqual(d.["重水"], 173)
    Assert.AreEqual(d.["液化臭氧"], 691)
    Assert.AreEqual(d.["锶包合物"], 173)