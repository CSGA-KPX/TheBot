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
open KPX.EvePlugin.Utils.Extensions
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
        let intManuPrice = ItemAccumulator<EveType>()

        for args in cmdArg.AllArgs do
            cfg.Parse(args)
            let str = cfg.GetNonOptionString()

            if not <| String.IsNullOrWhiteSpace(str) then
                match er.Eval(cfg.GetNonOptionString()) with
                | Number n -> cmdArg.Abort(InputError, "结算结果为数字: {0}", n)
                | Accumulator a ->
                    // BUG很大
                    // 需要全部求完以后再算
                    for mr in a.NegativeQuantityItems do
                        inv.Update(mr.Item, -mr.Quantity)

                    let intInv = MaterialInventory<_>(inv)

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

                    let pm2 = EveProcessManager(cfg.GetShiftedConfig())

                    // 更新中间产物
                    for info in proc.IntermediateProcess do
                        let intInv = MaterialInventory<_>(intInv)
                        let item = info.OriginProcess.Original.Product.Item
                        intProc.TryAdd(item, info.OriginProcess) |> ignore

                        let items = info.Quantity.ToItems(info.OriginProcess.Original)
                        let mr = RecipeMaterial<_>.Create (item, items)

                        // 计算直接材料，会用到的
                        let me =
                            if info.Depth = RecipeManager.DEPTH_PRODUCT then
                                cfg.InputMe
                            else
                                cfg.DerivationMe

                        let proc =
                            info
                                .OriginProcess
                                .Set(info.Quantity, me)
                                .ApplyFlags(MeApplied ProcessRunRounding.RoundUp)

                        for mr in proc.Materials do
                            intAcc.Update(mr)

                        // 因为每行配置可能不一行，只能单独计算再合并
                        // Rec模式下一次就会扣完的！
                        let intProc = pm2.GetMaterialsRec(Array.singleton mr, intInv)
                        let intMaterialPrice = intProc.FinalProcess.Materials.GetPrice(cfg.MaterialPriceMode)

                        let intInstallCost =
                            intProc.IntermediateProcess
                            |> Array.fold
                                (fun acc info ->
                                    acc
                                    + info
                                        .OriginProcess
                                        .SetQuantity(info.Quantity)
                                        .GetInstallationCost(cfg))
                                0.0

                        intManuPrice.Update(item, intMaterialPrice + intInstallCost)

                    // 生成产物表
                    for mr in proc.FinalProcess.Products do
                        outputAcc.Update(mr)

        // 产物也会出现在中间产物里，不再重复
        let intermediateTable =
            TextTable() {
                AsCols [ Literal "名称"
                         RLiteral "数量"
                         RLiteral "流程"
                         RLiteral(cfg.MaterialPriceMode.ToString())
                         RLiteral "制造" ]

                [ // intAcc含所有材料
                  // 和intManuPrice区交集
                  let mrs =
                      let intItems = intAcc.NonZeroItems |> Seq.map (fun x -> x.Item)
                      let manuItems = intManuPrice.NonZeroItems |> Seq.map (fun x -> x.Item)
                      let items = Set.intersect (Set.ofSeq intItems) (Set.ofSeq manuItems)
                      intAcc |> Seq.filter (fun mr -> items.Contains(mr.Item))

                  for mr in mrs do
                      let sellPrice = mr.GetPrice(cfg.MaterialPriceMode)
                      let manuCost = intManuPrice.[mr.Item]

                      [ Literal mr.Item.Name
                        Integer mr.Quantity
                        Integer(
                            (ByItems mr.Quantity)
                                .ToRuns(intProc.[mr.Item].Original)
                        )
                        HumanSig4(sellPrice) { boldIf (sellPrice <= manuCost) }
                        HumanSig4(manuCost) { boldIf (sellPrice > manuCost) } ] ]
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
