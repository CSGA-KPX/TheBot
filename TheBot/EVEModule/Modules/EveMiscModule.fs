namespace KPX.TheBot.Module.EveModule

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

open KPX.TheBot.Module.EveModule.Utils.Config

open KPX.TheBot.Data.EveData


type EveMiscModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("evehelp", "EVE星系成本指数查询", "")>]
    member x.HandleEvehelp(cmdArg : CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(Array.empty)

        let tt = TextTable("名称", "意义", "默认")
        tt.AddRow("ime", "输入蓝图材料效率", cfg.InputMe)
        tt.AddRow("dme", "衍生蓝图材料效率", cfg.DerivativetMe)
        tt.AddRow("sci", "星系成本指数", cfg.SystemCostIndex)
        tt.AddRow("tax", "设施税率", cfg.StructureTax)
        tt.AddRow("p", "设置后展开行星材料", cfg.ExpandPlanet)
        tt.AddRow("r", "设置后展开反应材料", cfg.ExpandReaction)
        tt.AddRow("buy", "设置后使用求购价格", cfg.MaterialPriceMode)
        tt.AddRow("text", "设置后使用文本输出（部分指令不支持）", cfg.IsDefined("text"))
        //tt.AddRow("selltax", "出售税率", 6)
        //tt.AddRow("buytax", "求购税率", 4)
        
        using (cmdArg.OpenResponse(ForceImage)) (fun ret -> ret.Write(tt))

    [<CommandHandlerMethodAttribute("evesci", "EVE星系成本指数查询", "")>]
    member x.HandleSci(cmdArg : CommandEventArgs) =
        let sc =
            SolarSystems.SolarSystemCollection.Instance

        let scc =
            SystemCostIndexCache.SystemCostIndexCollection.Instance

        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.Arguments)

        let tt =
            TextTable("星系", "制造%", "材料%", "时间%", "拷贝%", "发明%", "反应%")

        for arg in cfg.CommandLine do
            let sys = sc.TryGetBySolarSystem(arg)

            if sys.IsNone then
                tt.AddPreTable(sprintf "%s不是有效星系名称" arg)
            else
                let sci = scc.TryGetBySystem(sys.Value)

                if sci.IsNone then
                    tt.AddPreTable(sprintf "没有%s的指数信息" arg)
                else
                    let sci = sci.Value

                    tt.AddRow(
                        arg,
                        HumanReadableInteger(100.0 * sci.Manufacturing),
                        HumanReadableInteger(100.0 * sci.ResearcMaterial),
                        HumanReadableInteger(100.0 * sci.ResearchTime),
                        HumanReadableInteger(100.0 * sci.Copying),
                        HumanReadableInteger(100.0 * sci.Invention),
                        HumanReadableInteger(100.0 * sci.Reaction)
                    )

        using (cmdArg.OpenResponse(cfg.IsImageOutput)) (fun ret -> ret.Write(tt))
