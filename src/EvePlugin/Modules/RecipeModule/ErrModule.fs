namespace KPX.EvePlugin.Modules.Recipe.Err

open System

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Testing
open KPX.FsCqHttp.Utils.TextResponse

open KPX.TheBot.Host.DataModel.Recipe
open KPX.TheBot.Host.Utils.RecipeRPN

open KPX.EvePlugin.Data.EveType
open KPX.EvePlugin.Data.Process

open KPX.EvePlugin.Utils
open KPX.EvePlugin.Utils.Config
open KPX.EvePlugin.Utils.UserInventory


type ERRModule() =
    inherit CommandHandlerBase()

    let er = EveExpression.EveExpression()
    let ic = InventoryCollection.Instance

    [<CommandHandlerMethod("#er",
                           "EVE蓝图材料计算",
                           "可以使用表达式，多个物品需用+连接。可选参数见#evehelp。如：
#er 帝国海军散热槽*10+机器人技术*9999")>]
    [<CommandHandlerMethod("#err",
                           "EVE蓝图基础材料计算",
                           "可以使用表达式，多个物品需用+连接。可选参数见#evehelp。如：
#err 帝国海军散热槽*10+机器人技术*9999")>]
    member x.HandleRRR(cmdArg: CommandEventArgs) =
        // 材料计算流程优先
        let cfg = EveConfigParser(RunRounding = ProcessRunRounding.RoundUp)
        // 默认值必须是不可能存在的值，比如空格
        let idOpt = cfg.RegisterOption<string>("id", "\r\n")
        cfg.Parse(cmdArg.HeaderArgs)
        // 下面for循环中会被覆写
        let respType = cfg.ResponseType

        let useInv, inv =
            match idOpt.IsDefined with
            | false -> false, MaterialInventory<EveType>()
            | true when ic.Contains(idOpt.Value) -> true, snd (ic.TryGet(idOpt.Value).Value)
            | true -> cmdArg.Abort(InputError, "没有和id关联的材料表")

        let inputAcc = ItemAccumulator<EveType>()
        let outputAcc = ItemAccumulator<EveType>()

        let intAcc = ItemAccumulator<EveType>()
        let intProc = Collections.Generic.Dictionary<EveType, EveProcess>()

        for args in cmdArg.AllArgs do
            cfg.Parse(args)
            let str = cfg.GetNonOptionString()

            if not <| String.IsNullOrWhiteSpace(str) then
                match er.Eval(cfg.GetNonOptionString()) with
                | Number n -> cmdArg.Abort(InputError, "结算结果为数字: {0}", n)
                | Accumulator a ->
                    for mr in a.NegativeQuantityItems do
                        inv.Update(mr.Item, -mr.Quantity)

                    let proc =
                        let pm = EveProcessManager(cfg)
                        let expandProc = cmdArg.CommandAttrib.Command = "#err"
                        let a = a.PositiveQuantityItems

                        if expandProc then
                            pm.GetMaterialsRec(a, inv)
                        else
                            pm.GetMaterialsRec(a, inv, depthLimit = 1)

                    // 更新材料
                    for mr in proc.FinalProcess.Materials do
                        inputAcc.Update(mr)

                    // 更新中间产物
                    for (quantity, proc, _) in proc.IntermediateProcess do
                        intProc.TryAdd(proc.Original.Product.Item, proc) |> ignore
                        let items = quantity.ToItems(proc.Original)
                        intAcc.Update(proc.Original.Product.Item, items)

                    // 生成产物表
                    for mr in proc.FinalProcess.Products do
                        outputAcc.Update(mr)

        // 产物也会出现在中间产物里，不再重复
        let intermediateTable =
            TextTable() {
                AsCols [ Literal "名称"
                         RLiteral "数量"
                         RLiteral "流程" ]

                [ for mr in intAcc.NonZeroItems do
                      [ Literal mr.Item.Name
                        Integer mr.Quantity
                        Integer(
                            (ByItems mr.Quantity)
                                .ToRuns(intProc.[mr.Item].Original)
                        ) ] ]

            }

        let mainTable =
            TextTable(respType) {
                intermediateTable

                AsCols [ Literal "名称"
                         if useInv then
                             RLiteral "缺少"
                         else
                             RLiteral "数量"
                         RLiteral "体积" ]
            }

        // 生成材料信息
        let mutable totalInputVolume = 0.0

        for mr in inputAcc.NonZeroItems |> Seq.sortBy (fun x -> x.Item.MarketGroupId) do
            let sumVolume = mr.Item.Volume * mr.Quantity

            mainTable {
                AsCols [ Literal mr.Item.Name
                         Integer mr.Quantity
                         Integer sumVolume ]
            }
            |> ignore

            totalInputVolume <- totalInputVolume + sumVolume

        // 总材料信息
        mainTable {
            AsCols [ Literal "材料体积"
                     RightPad
                     CellUtils.Number totalInputVolume ]
        }
        |> ignore

        if inputAcc.Count = 0 then
            cmdArg.Abort(InputError, "计算结果为空")

        mainTable

    [<TestFixture>]
    member x.TestRRR() =
        let tc = TestContext(x)
        tc.ShouldThrow("#er")
        tc.ShouldThrow("#er 5*5")
        tc.ShouldThrow("#er 军用馒头 ime:10")
        tc.ShouldThrow("#er 军用馒头蓝图")

        tc.ShouldNotThrow("#er 恶狼级")
        tc.ShouldNotThrow("#er 恶狼级蓝图 ime:10")

        tc.ShouldThrow("#err")
        tc.ShouldThrow("#err 5*5")
        tc.ShouldThrow("#err 军用馒头 ime:10")
        tc.ShouldThrow("#err 军用馒头蓝图")

        tc.ShouldNotThrow("#err 恶狼级")
        tc.ShouldNotThrow("#err 恶狼级蓝图 ime:10")
