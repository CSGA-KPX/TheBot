namespace TheBot.Module.EveModule
open System
open System.Collections.Generic
open System.Text
open KPX.FsCqHttp.Handler.CommandHandlerBase
open TheBot.Utils.HandlerUtils
open TheBot.Utils.TextTable
open TheBot.Module.EveModule.Utils
open EveData

type EveModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("updateevedb", "刷新价格数据库（管理员）", "")>]
        member x.HandleRefreshCache(msgArg : CommandArgs) =
            failOnNonAdmin(msgArg)
            let updated = UpdatePriceCache()
            msgArg.QuickMessageReply(sprintf "价格缓存刷新成功 @ %O" updated)

    [<CommandHandlerMethodAttribute("em", "查询物品价格", "")>]
        member x.HandleEveMarket(msgArg : CommandArgs) =
            let name = String.Join(" ", msgArg.Arguments)
            let succ, item = EveTypeNameCache.TryGetValue(name)
            if not succ then
                failwith "找不到物品"
            else
                let sell = GetItemPriceCached(item.TypeId)
                msgArg.QuickMessageReply(String.Format("物品{0} 出售价格：{1:N0}", item.TypeName, sell))

    [<CommandHandlerMethodAttribute("eme", "EVE蓝图材料效率计算", "")>]
        member x.HandleME(msgArg : CommandArgs) =
            let name = String.Join(" ", msgArg.Arguments)
            let succ, item = EveTypeNameCache.TryGetValue(name)
            if succ then
                let succ, bp = EveBlueprintCache.TryGetValue(item.TypeId)
                if succ then
                    let me0 = bp.AdjustMaterialsByME(0)
                    let me0Price = 
                        me0
                        |> Array.map (fun m -> (float m.Quantity) * (GetItemPriceCached m.TypeId))
                        |> Array.sum

                    let att = AutoTextTable<int>(
                                [|
                                    "材料等级", fun me -> box(me)
                                    "节省", fun me -> 
                                                    bp.AdjustMaterialsByME(me)
                                                    |> Array.map (fun m -> (float m.Quantity) * (GetItemPriceCached m.TypeId))
                                                    |> Array.sum
                                                    |> (fun x -> (me0Price - x))
                                                    |> box
                                |]
                    )
                    att.AddPreTable(sprintf "物品 %s" item.TypeName)
                    att.AddPreTable("直接材料总价：" + System.String.Format("{0:N0}", me0Price))
                    for i = 0 to 10 do
                        att.AddObject(i)
                    msgArg.QuickMessageReply(att.ToString())
                else
                    failwithf "找不到蓝图信息 '%s'" name
            else
                failwithf "找不到物品 '%s'" name
            ()

    [<CommandHandlerMethodAttribute("er", "EVE蓝图材料计算", "")>]
        member x.HandleR(msgArg : CommandArgs) =
            let (name, cfg) = ScanConfig(msgArg.Arguments)

            let succ, item = EveTypeNameCache.TryGetValue(name)
            if not succ then
                failwithf "找不到物品"
            let succ, bp = EveBlueprintCache.TryGetValue(item.TypeId)
            if not succ then
                failwithf "找不到蓝图信息'"

            let tt = TextTable.FromHeader([|"名称"; "数量";|])

            tt.AddPreTable(sprintf "材料效率：%i%% 设施调整：%i%% " cfg.MaterialEfficiency cfg.StructureBonuses)
            
            let runs = cfg.GetRuns(bp)
            tt.AddPreTable(sprintf "设定流程：%i 设定个数：%i 最终流程：%f" cfg.InitRuns cfg.InitItems runs)

            let final = 
                bp.AdjustMaterialsByME(cfg.MaterialEfficiency)
                |> Array.map (fun m -> {m with Quantity = m.Quantity * (float runs) |> ceil })

            for m in final do 
                tt.AddRow(EveTypeIdCache.[m.TypeId].TypeName, m.Quantity)

            msgArg.QuickMessageReply(tt.ToString())

    [<CommandHandlerMethodAttribute("err", "EVE蓝图材料计算", "")>]
        member x.HandleRR(msgArg : CommandArgs) =
            let (name, cfg) = ScanConfig(msgArg.Arguments)

            let succ, item = EveTypeNameCache.TryGetValue(name)
            if not succ then
                failwithf "找不到物品"
            let succ, bp = EveBlueprintCache.TryGetValue(item.TypeId)
            if not succ then
                failwithf "找不到蓝图信息'"

            let tt = TextTable.FromHeader([|"名称"; "数量";|])

            tt.AddPreTable(sprintf "材料效率：%i%% 设施调整：%i%% " cfg.MaterialEfficiency cfg.StructureBonuses)

            let final = FinalMaterials()

            let rec loop (bp : EveData.EveBlueprint, runs : float) = 
                let ms =
                    bp.AdjustMaterialsByME(cfg.MaterialEfficiency)
                    |> Array.map (fun m -> {m with Quantity = m.Quantity * runs |> ceil })

                for m in ms do 
                    let next, bp = itemToBp.TryGetValue(m.TypeId)
                    if next then
                        let p = bp.Products |> Array.head
                        let runs = m.Quantity / p.Quantity
                        loop(bp, runs)
                    else
                        final.AddOrUpdate(EveTypeIdCache.[m.TypeId], m.Quantity)

            let runs = cfg.GetRuns(bp)
            tt.AddPreTable(sprintf "设定流程：%i 设定个数：%i 最终流程：%f" cfg.InitRuns cfg.InitItems runs)

            loop(bp, runs)

            for (t, q) in final.Get() do 
                tt.AddRow(t.TypeName, q)

            msgArg.QuickMessageReply(tt.ToString())

    [<CommandHandlerMethodAttribute("errc", "EVE蓝图成本计算", "")>]
        member x.HandleRRC(msgArg : CommandArgs) =
            let (name, cfg) = ScanConfig(msgArg.Arguments)
            let succ, item = EveTypeNameCache.TryGetValue(name)
            if not succ then
                failwithf "找不到物品"
            let succ, bp = EveBlueprintCache.TryGetValue(item.TypeId)
            if not succ then
                failwithf "找不到蓝图信息'"

            let tt = TextTable.FromHeader([|"组件（无基本材料）"; "买成品"; "搓（材料+费）"|])

            tt.AddPreTable("价格有延迟，算法不稳定，市场有风险, 投资需谨慎")
            tt.AddPreTable(sprintf "材料效率：%i%% 成本指数：%i%% 设施调整：%i%% 设施税率%i%%"
                cfg.MaterialEfficiency
                cfg.SystemCostIndex
                cfg.StructureBonuses
                cfg.StructureTax
            )

            let mutable sum = 0.0

            let final = FinalMaterials()

            let rec loop (bp : EveData.EveBlueprint, runs : float) = 
                let ms =
                    bp.AdjustMaterialsByME(cfg.MaterialEfficiency)
                    |> Array.map (fun m -> {m with Quantity = m.Quantity * runs |> ceil })

                let total = 
                    ms
                    |> Array.map (fun m ->
                        let p = (GetItemPriceCached m.TypeId)
                        m.Quantity * p)
                    |> Array.sum

                let fee = total * (pct cfg.SystemCostIndex) * (pct cfg.StructureBonuses)
                let tax = fee * (pct cfg.StructureTax)
                let cost = fee + tax

                let tid = (bp.Products |> Array.head).TypeId
                let p = 
                    EveTypeIdCache.[tid].TypeName

                printfn "Adding cost %s * %f : %f" p runs cost
                sum <- sum + cost
                let sell = (GetItemPriceCached tid) * runs
                let products = (bp.Products |> Array.head).Quantity
                tt.AddRow(p, sell * products, total + cost)
                
                for m in ms do 
                    let next, bp = itemToBp.TryGetValue(m.TypeId)
                    if next then
                        let p = bp.Products |> Array.head
                        let runs = m.Quantity / p.Quantity
                        loop(bp, runs)
                    else
                        let item = EveTypeIdCache.[m.TypeId]
                        final.AddOrUpdate(item, m.Quantity)
                        printfn "Adding %s : %f" item.TypeName m.Quantity
                        sum <- sum + GetItemPriceCached(m.TypeId) * (float m.Quantity)

            let runs = cfg.GetRuns(bp)
            tt.AddPreTable(sprintf "设定流程：%i 设定个数：%i 最终流程：%f" cfg.InitRuns cfg.InitItems runs)

            loop(bp, runs)

            tt.AddRowPadding("材料+费用合计", "--", sum)

            //let basic = TextTable.FromHeader([|"基本材料"; "数量"; "单价"; "小计"|])
            //let mutable basicSum = 0.0
            //for (item, q) in final.Get() do 
            //    let price = GetItemPriceCached(item.TypeId)
            //    let total = price * q
            //    basic.AddRow(item.TypeName, q, price, total)
            //tt.AddPostTable(basic)

            msgArg.QuickMessageReply(tt.ToString())

    [<CommandHandlerMethodAttribute("EVE压矿", "EVE蓝图成本计算", "")>]
        member x.HandleOreCompression(msgArg : CommandArgs) =
            let ores = 
                EveTypeNameCache.Values
                |> Seq.filter (fun x -> x.TypeName.StartsWith("高密度"))
            let tt = TextTable.FromHeader([|"矿"; "压缩前（1化矿单位）"; "压缩后"; "溢价比"|])
            for ore in ores do
                if not <| ore.TypeName.Contains("冰") then
                    let compressed = ore
                    let normal     = EveTypeNameCache.[ore.TypeName.[3..]]

                    let cp = GetItemPriceCached(compressed.TypeId)
                    let np = GetItemPriceCached(normal.TypeId)

                    if np <> 0.0 then
                        tt.AddRow(normal.TypeName, np*100.0, cp, cp/np/100.0)
            msgArg.QuickMessageReply(tt.ToString())

        [<CommandHandlerMethodAttribute("errc2", "EVE蓝图成本计算", "")>]
        member x.HandleERRCV2(msgArg : CommandArgs) = 
            let (name, cfg) = ScanConfig(msgArg.Arguments)
            let succ, item = EveTypeNameCache.TryGetValue(name)
            if not succ then
                failwithf "找不到物品"

            let bp = 
                let succ, bp = EveBlueprintCache.TryGetValue(item.TypeId)
                if not succ then
                    let succ, bp = itemToBp.TryGetValue(item.TypeId)
                    if not succ then
                        failwithf "找不到蓝图信息'"
                    else
                        // 输入是物品，但是存在制造蓝图
                        bp
                else
                    // 输入是蓝图
                    bp

            let tt = TextTable.FromHeader([|"组件（无基本材料）"; "买成品"; "搓（材料+费）"|])
            tt.AddPreTable("价格有延迟，算法不稳定，市场有风险, 投资需谨慎")
            tt.AddPreTable(sprintf "材料效率：%i%% 成本指数：%i%% 设施调整：%i%% 设施税率%i%%"
                cfg.MaterialEfficiency
                cfg.SystemCostIndex
                cfg.StructureBonuses
                cfg.StructureTax
            )

            let getFee price = 
                let fee = price * (pct cfg.SystemCostIndex) * (pct cfg.StructureBonuses)
                let tax = fee * (pct cfg.StructureTax)
                fee + tax
  
            let rec getRootMaterialsPrice (bp : EveBlueprint) (need : float) = 
                let pp = bp.Products |> Array.head
                let runs = need / pp.Quantity
                let ms = bp.AdjustMaterialsByME(cfg.DefME)

                let mutable sum = 0.0

                for m in ms do 
                    let amount = m.Quantity * runs |> ceil
                    let price = GetItemPriceCached(m.TypeId) * amount
                    let fee = getFee(price)
                    sum <- sum + price + fee

                    let succ, bp = itemToBp.TryGetValue(m.TypeId)
                    if succ then
                        //有蓝图
                        let p = bp.Products |> Array.head
          
                        sum <- sum + getRootMaterialsPrice bp amount
                sum

            let mutable optCost = 0.0
            // 所有材料
            // 材料名称 数量 售价（小计） 制造成本（小计） 最佳成本（小计）
            let ms, runs = bp.AdjustMaterialsByME(cfg.InputME), cfg.GetRuns(bp)
            for m in ms do 
                let amount = m.Quantity * runs |> ceil
                let name   = EveTypeIdCache.[m.TypeId].TypeName
                let buy = GetItemPriceCached(m.TypeId) * amount

                let succ, bp = itemToBp.TryGetValue(m.TypeId)
                if succ then
                    let cost = getRootMaterialsPrice bp amount
                    optCost <- optCost + (if cost >= buy then buy else cost)
                    tt.AddRow(name, buy, cost |> ceil)
                else
                    optCost <- optCost + buy
                    tt.AddRow(name, buy, "--")

            let sell = GetItemPriceCached((bp.Products |> Array.head).TypeId) * (cfg.GetItems(bp))

            tt.AddRowPadding("售价/最佳造价", sell |> ceil, optCost |> ceil)
            msgArg.QuickMessageReply(tt.ToString())