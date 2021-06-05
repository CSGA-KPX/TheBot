module EveTest

open KPX.TheBot.Data.CommonModule.Recipe

open KPX.TheBot.Data.EveData.EveType
open KPX.TheBot.Data.EveData.Process

open Expecto


let private ec = EveTypeCollection.Instance

let private rm =
    EveProcessManager(
        { new IEveCalculatorConfig with
            member x.InputMe = 3
            member x.DerivationMe = 10
            member x.ExpandPlanet = false
            member x.ExpandReaction = false }
    )

let private process2Dict (proc : RecipeProcess<EveType>) =
    seq {
        for i in proc.Input do
            yield i.Item.Name, i.Quantity |> int

        for i in proc.Output do
            yield i.Item.Name, i.Quantity |> int
    }
    |> readOnlyDict

let eveTests =
    testList
        "EVE测试集"
        [ testCase "物品转换"
          <| fun _ ->
              Expect.equal (ec.GetById(34).Name) "三钛合金" ""
              Expect.equal (ec.GetByName("三钛合金").Id) 34 ""
              Expect.equal (ec.GetByName("三钛合金").Name) "三钛合金" ""
              ()
              
          testCase "输入蓝图，材料效率2"
          <| fun _ ->
                let ret =
                    rm.TryGetRecipe(ec.TryGetByName("麦基诺级蓝图").Value, ByItem 1.0)

                Expect.isSome ret ""
                let d = process2Dict (ret.Value.ApplyFlags(ProcessFlag.MeApplied))
                Expect.equal d.["建筑模块"] 146 ""
                Expect.equal d.["莫尔石"] 107 ""
                Expect.equal d.["R.A.M. - 星舰科技"] 15 ""
                Expect.equal d.["离子推进器"] 59 ""
                Expect.equal d.["磁力感应器组"] 219 ""
                Expect.equal d.["光子微处理器"] 2910 ""
                Expect.equal d.["碳化晶体附甲"] 2910 ""
                Expect.equal d.["聚变反应堆机组"] 44 ""
                Expect.equal d.["震荡电容器单元"] 582 ""
                Expect.equal d.["脉冲护盾发射器"] 219 ""
                Expect.equal d.["回旋者级"] 1 ""
                
          testCase "输入物品，材料效率2"
          <| fun _ ->
                let ret =
                    rm.TryGetRecipe(ec.TryGetByName("麦基诺级").Value, ByItem 1.0)

                Expect.isSome ret ""
                let d = process2Dict (ret.Value.ApplyFlags(ProcessFlag.MeApplied))
                Expect.equal d.["建筑模块"] 146 ""
                Expect.equal d.["莫尔石"] 107 ""
                Expect.equal d.["R.A.M. - 星舰科技"] 15 ""
                Expect.equal d.["离子推进器"] 59 ""
                Expect.equal d.["磁力感应器组"] 219 ""
                Expect.equal d.["光子微处理器"] 2910 ""
                Expect.equal d.["碳化晶体附甲"] 2910 ""
                Expect.equal d.["聚变反应堆机组"] 44 ""
                Expect.equal d.["震荡电容器单元"] 582 ""
                Expect.equal d.["脉冲护盾发射器"] 219 ""
                Expect.equal d.["回旋者级"] 1 ""
                
          testCase "输入物品，递归，材料效率2"
          <| fun _ ->
                // TODO 换成T2战巡
                let ret =
                    rm.TryGetRecipeRecMe(ec.TryGetByName("麦基诺级").Value, ByItem 1.0)

                Expect.isSome ret ""

                let d =
                    process2Dict ret.Value.FinalProcess

                for kv in d do
                    System.Console.WriteLine("{0} : {1}", kv.Key, kv.Value)

                Expect.equal d.["建筑模块"] 146 ""
                Expect.equal d.["莫尔石"] 107 ""
                Expect.equal d.["三钛合金"] 1440075 ""
                Expect.equal d.["类晶体胶矿"] 270060 ""
                Expect.equal d.["类银超金属"] 67530 ""
                Expect.equal d.["同位聚合体"] 36011 ""
                Expect.equal d.["超新星诺克石"] 13504 ""
                Expect.equal d.["酚合成物"] 17637 ""
                Expect.equal d.["铁磁胶体"] 278 ""
                Expect.equal d.["碳化晶体"] 187374 ""
                Expect.equal d.["纳米晶体管"] 6621 ""
                Expect.equal d.["超级突触纤维"] 438 ""
                Expect.equal d.["光子超材料"] 6984 ""
                Expect.equal d.["多晶碳化硅纤维"] 31071 ""
                Expect.equal d.["费米子冷凝物"] 88 ""
                Expect.equal d.["富勒化合物"] 5820 ""
                Expect.equal d.["晶状石英核岩"] 2250 ""
                Expect.equal d.["超噬矿"] 1260 ""
                
          testCase "精炼矿石"
          <| fun _ ->
                let ore = ec.TryGetByName("凡晶石").Value

                let refine =
                    RefineProcessCollection.Instance.GetProcessFor(ore)

                let d = process2Dict (refine.ApplyFlags(ProcessFlag.MeApplied))
                System.Console.Write $"%A{refine}"
                Expect.equal d.["三钛合金"] 400 ""
              
          testCase "精炼冰矿"
          <| fun _ ->
                let ore = ec.TryGetByName("冰晶矿").Value

                let refine =
                    RefineProcessCollection.Instance.GetProcessFor(ore)

                System.Console.Write $"%A{refine}"
                let d = process2Dict (refine.ApplyFlags(ProcessFlag.MeApplied))
                Expect.equal d.["重水"] 173 ""
                Expect.equal d.["液化臭氧"] 691 ""
                Expect.equal d.["锶包合物"] 173 ""
        ]
