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
                msgArg.QuickMessageReply(String.Format("{0} 出售价格：{1:N2}", item.TypeName, sell))

    [<CommandHandlerMethodAttribute("emr", "实时价格查询（市场中心API）", "")>]
        member x.HandleEveMarketR(msgArg : CommandArgs) =
            let name = String.Join(" ", msgArg.Arguments)
            let succ, item = EveTypeNameCache.TryGetValue(name)
            if not succ then
                failwith "找不到物品"
            else
                let sell, buy = GetItemPrice(item.TypeId)
                msgArg.QuickMessageReply(String.Format("{0} 出售：{1:N2} 买入：{2:N2}", item.TypeName, sell, buy))

    [<CommandHandlerMethodAttribute("eme", "EVE蓝图材料效率计算", "")>]
        member x.HandleME(msgArg : CommandArgs) =
            let name = String.Join(" ", msgArg.Arguments)
            let succ, item = EveTypeNameCache.TryGetValue(name)
            if succ then
                let succ, bp = EveBlueprintCache.TryGetValue(item.TypeId)
                if succ then
                    let me0 = bp.ApplyMaterialEfficiency(0).Materials
                    let me0Price = 
                        me0
                        |> Array.map (fun m -> (float m.Quantity) * (GetItemPriceCached m.TypeId))
                        |> Array.sum

                    let att = AutoTextTable<int>(
                                [|
                                    "材料等级", fun me -> box(me)
                                    "节省", fun me -> 
                                                    bp.ApplyMaterialEfficiency(me).Materials
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

            let finalBp = cfg.ConfigureBlueprint(bp)
            tt.AddPreTable(sprintf "输入效率：%i%% 默认效率：%i%% 产品个数:%g"
                cfg.InputME
                cfg.DefME
                finalBp.ProductQuantity
            )

            for m in finalBp.Materials do 
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

            let finalBp = cfg.ConfigureBlueprint(bp)
            tt.AddPreTable(sprintf "输入效率：%i%% 默认效率：%i%% 产品个数:%g"
                cfg.InputME
                cfg.DefME
                finalBp.ProductQuantity
            )

            let final = FinalMaterials()
            let rec loop (bp : EveData.EveBlueprint) = 
                for m in bp.Materials do 
                    let next, bp = itemToBp.TryGetValue(m.TypeId)
                    if next then
                        let bp = bp.ApplyMaterialEfficiency(cfg.DefME).GetBlueprintByItems(m.Quantity)
                        loop(bp)
                    else
                        final.AddOrUpdate(EveTypeIdCache.[m.TypeId], m.Quantity)

            loop(finalBp)

            for (t, q) in final.Get() do 
                tt.AddRow(t.TypeName, q)

            msgArg.QuickMessageReply(tt.ToString())

    [<CommandHandlerMethodAttribute("EVE压矿", "EVE压矿利润", "")>]
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

    [<CommandHandlerMethodAttribute("EVE采矿", "EVE挖矿利润", "")>]
    [<CommandHandlerMethodAttribute("EVE挖矿", "EVE挖矿利润", "")>]
        member x.HandleOreMining(msgArg : CommandArgs) =
            let mineSpeed = 10.0 // m^3/s
            let refineYield = 0.70

            let tt = TextTable.FromHeader([|"矿石"; "秒利润"; "冰矿"; "秒利润"; "月矿"; "秒利润";|])
            tt.AddPreTable(sprintf "采集能力：%g m3/s 精炼效率:%g"
                mineSpeed
                refineYield
            )
            
            let getSubTypes (names : string) = 
                names.Split(',')
                |> Array.map (fun name ->
                    let info = OreRefineInfo.[name]
                    let refinePerSec = mineSpeed / info.Volume / info.RefineUnit
                    let price =
                        info.Yields
                        |> Array.sumBy (fun m -> 
                            m.Quantity * refinePerSec * refineYield * GetItemPriceCached(m.TypeId))
                    name, price|> ceil )
                |> Array.sortByDescending snd

            let moon= getSubTypes EveData.MoonNames
            let ice = getSubTypes EveData.IceNames
            let ore = getSubTypes EveData.OreNames

            let tryGetRow (arr : (string * float) [])  (id : int)  = 
                if id <= arr.Length - 1 then
                    let n,p = arr.[id]
                    (box n, box p)
                else
                    (box "--", box "--")

            let rowMax = (max (max ice.Length moon.Length) ore.Length) - 1
            for i = 0 to rowMax do 
                let eon, eop = tryGetRow ore i
                let ein, eip = tryGetRow ice i
                let emn, emp = tryGetRow moon i
                tt.AddRow(eon, eop, ein, eip, emn, emp)

            msgArg.QuickMessageReply(tt.ToString())

        [<CommandHandlerMethodAttribute("errc", "EVE蓝图成本计算", "")>]
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

            let finalBp = cfg.ConfigureBlueprint(bp)

            let tt = TextTable.FromHeader([|"组件"; "数量"; "买成品"; "搓（材料+费）"|])
            tt.AddPreTable("价格有延迟，算法不稳定，市场有风险, 投资需谨慎")
            tt.AddPreTable(sprintf "输入效率：%i%% 默认效率：%i%% 成本指数：%i%% 设施税率%i%% 产品个数:%g"
                cfg.InputME
                cfg.DefME
                cfg.SystemCostIndex
                cfg.StructureTax
                finalBp.ProductQuantity
            )

            let getFee price = 
                let fee = price * (pct cfg.SystemCostIndex) * (pct cfg.StructureBonuses)
                let tax = fee * (pct cfg.StructureTax)
                fee + tax

            let rec getRootMaterialsPrice (bp : EveBlueprint) = 
                let mutable sum = 0.0

                for m in bp.Materials do 
                    let amount = m.Quantity
                    let price = GetItemPriceCached(m.TypeId) * amount
                    let fee = getFee(price)
                    sum <- sum + fee

                    let succ, bp = itemToBp.TryGetValue(m.TypeId)
                    if succ then
                        let bp = bp.ApplyMaterialEfficiency(cfg.DefME).GetBlueprintByItems(amount)
                        sum <- sum + getRootMaterialsPrice bp
                    else
                        sum <- sum + price
                sum

            let mutable optCost = 0.0
            let mutable allCost = 0.0

            // 所有材料
            // 材料名称 数量 售价（小计） 制造成本（小计） 最佳成本（小计）
            for m in finalBp.Materials do 
                let amount = m.Quantity
                let name   = EveTypeIdCache.[m.TypeId].TypeName
                let buy    = GetItemPriceCached(m.TypeId) * amount
                
                optCost <- optCost + getFee(buy)
                allCost <- allCost + getFee(buy)

                let succ, bp = itemToBp.TryGetValue(m.TypeId)
                if succ then
                    let bp = bp.ApplyMaterialEfficiency(cfg.DefME).GetBlueprintByItems(amount)
                    let cost = getRootMaterialsPrice bp
                    optCost <- optCost + (if (cost >= buy) && (buy <> 0.0) then buy else cost)
                    allCost <- allCost + cost
                    tt.AddRow(name, amount, buy, cost |> ceil)
                else
                    optCost <- optCost + buy
                    allCost <- allCost + buy
                    tt.AddRow(name, amount, buy, "--")

            let sell = GetItemPriceCached((bp.Products |> Array.head).TypeId) * (finalBp.ProductQuantity)

            tt.AddRowPadding("售价/最佳/造价", sell |> ceil, optCost |> ceil, allCost |> ceil)
            msgArg.QuickMessageReply(tt.ToString())