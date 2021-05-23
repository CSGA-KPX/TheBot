namespace KPX.TheBot.Module.EveModule

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Testing
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

open KPX.TheBot.Module.EveModule.Utils.Config

open KPX.TheBot.Data.EveData

open KPX.TheBot.Utils.EmbeddedResource


type CombatSiteInfo =
    { Type : string
      FoundIn : string
      Difficulty : string
      Name : string }

type EveMiscModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("#evehelp", "EVE星系成本指数查询", "")>]
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

        using (cmdArg.OpenResponse(ForceImage)) (fun ret -> ret.Write(tt))

    [<TestFixture>]
    member x.TestEveHelp() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#evehelp")

    [<CommandHandlerMethodAttribute("#evesci", "EVE星系成本指数查询", "")>]
    member x.HandleSci(cmdArg : CommandEventArgs) =
        let sc =
            SolarSystems.SolarSystemCollection.Instance

        let scc =
            SystemCostIndexCache.SystemCostIndexCollection.Instance

        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.Arguments)

        let tt =
            TextTable("星系", "制造%", "材料%", "时间%", "拷贝%", "发明%", "反应%")

        for arg in cfg.NonOptionStrings do
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
                        HumanReadableSig4Float(100.0 * sci.Manufacturing),
                        HumanReadableSig4Float(100.0 * sci.ResearcMaterial),
                        HumanReadableSig4Float(100.0 * sci.ResearchTime),
                        HumanReadableSig4Float(100.0 * sci.Copying),
                        HumanReadableSig4Float(100.0 * sci.Invention),
                        HumanReadableSig4Float(100.0 * sci.Reaction)
                    )

        using (cmdArg.OpenResponse(cfg.ResponseType)) (fun ret -> ret.Write(tt))

    [<TestFixture>]
    member x.TestSystemCostIndex() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#evesci 吉他 皮尔米特")

    member val EveCombatSites =
        [| let mgr =
               StringResource("EVE").GetLines("战斗空间信号")
               |> Array.map (fun line -> line.Split('\t'))

           for line in mgr do
               let t = line.[0]
               let f = line.[1]
               let d = line.[2]

               for i = 3 to line.Length - 1 do
                   let n = line.[i]
                   if n <> "-" then
                       yield
                           { CombatSiteInfo.Type = t
                             CombatSiteInfo.Difficulty = d
                             CombatSiteInfo.FoundIn = f
                             CombatSiteInfo.Name = n } |]

    [<CommandHandlerMethodAttribute("#eve异常", "EVE异常/死亡表 可接信号名称", "")>]
    [<CommandHandlerMethodAttribute("#eve死亡", "EVE异常/死亡表 可接信号名称", "")>]
    member x.HandleUnrated(cmdArg : CommandEventArgs) =
        if cmdArg.Arguments.Length = 0 then
            let mgr =
                StringResource("EVE").GetLines("战斗空间信号")
                |> Array.map (fun line -> line.Split('\t') |> Array.map box)

            let tt = TextTable(mgr.[0])

            for i = 1 to mgr.Length - 1 do
                tt.AddRow(mgr.[i])

            using (cmdArg.OpenResponse(ForceImage)) (fun ret -> ret.Write(tt))
        else
            use ret = cmdArg.OpenResponse(ForceText)

            for arg in cmdArg.Arguments do
                let mutable found = false

                for site in x.EveCombatSites do
                    if site.Name = arg then
                        found <- true
                        ret.WriteLine("{0}：", arg)
                        ret.WriteLine("	类型：{0}", site.Type)
                        ret.WriteLine("	难度：{0}", site.Difficulty)
                        ret.WriteLine("	安等：{0}", site.FoundIn)

                if not found then ret.WriteLine("{0}：未找到相关信息", arg)
