namespace KPX.TheBot.Module.EveModule

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

open KPX.TheBot.Module.EveModule.Utils.Config

open KPX.TheBot.Data.EveData


type EveMiscModule() =
    inherit CommandHandlerBase()

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
