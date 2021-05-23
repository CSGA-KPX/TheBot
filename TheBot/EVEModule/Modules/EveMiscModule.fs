namespace KPX.TheBot.Module.EveModule

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Testing
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

open KPX.TheBot.Module.EveModule.Utils.Config

open KPX.TheBot.Data.EveData


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

    [<CommandHandlerMethodAttribute("#eve异常", "EVE异常表", "")>]
    member x.HandleAnomalies(cmdArg : CommandEventArgs) =
        let tt = TextTable("等级/衍生", "出现", "海盗", "无人机")
        tt.AddRow(" 1/1", "高", "隐蔽处", "无人机群")
        tt.AddRow(" 1/2", "高", "隐秘的藏身处", "无人机群")
        tt.AddRow(" 1/3", "高", "废弃的藏身处", "无人机群")
        tt.AddRow(" 1/4", "高", "荒废的藏身处", "无人机群")

        tt.AddRow(" 2/1", "高", "藏身处/天使洞穴", "无人机集群")

        tt.AddRow(" 3/1", "高低", "庇护所", "无人机集结")

        tt.AddRow(" 4/1", "高低", "贼窝", "无人机聚集")
        tt.AddRow(" 4/2", "低", "隐匿的据点", "无人机聚集")
        tt.AddRow(" 4/3", "低", "遗忘的隐藏所", "无人机聚集")
        tt.AddRow(" 4/4", "低", "荒废的据点", "无人机聚集")

        tt.AddRow(" 5/1", "低", "船坞", "无人机监查")

        tt.AddRow(" 6/1", "低零", "集会点", "无人机群落")
        tt.AddRow(" 6/2", "低零", "隐秘的集合地", "无人机群落")
        tt.AddRow(" 6/3", "低零", "废弃的集合地", "无人机群落")
        tt.AddRow(" 6/4", "低零", "荒废的集合地", "无人机群落")

        tt.AddRow(" 7/1", "低零", "港", "无人机团")

        tt.AddRow(" 8/1", "低零", "活动中心", "无人机小队")
        tt.AddRow(" 8/2", "低零", "隐秘老巢", "无人机小队")
        tt.AddRow(" 8/3", "低零", "离弃老巢", "无人机小队")
        tt.AddRow(" 8/4", "低零", "遗弃的老巢", "无人机小队")

        tt.AddRow(" 9/1", "零", "避难所", "无人机巡逻队")

        tt.AddRow("10/1", "零", "圣坛", "无人机群")

        using (cmdArg.OpenResponse(ForceImage)) (fun ret -> ret.Write(tt))

    [<CommandHandlerMethodAttribute("#eve死亡", "EVE未分级死亡表", "")>]
    member x.HandleUnrated(cmdArg : CommandEventArgs) =
        let tt = TextTable("高", "低", "零", "海盗", "无人机")
        tt.AddRow(" ✔", " ✔", "", "隐蔽所", "闹鬼庭院")
        tt.AddRow(" ✔", " ✔", "", "哨所", "凄凉小站")
        tt.AddRow(" ✔", " ✔", "", "瞭望站", "化学园区")
        tt.AddRow(" ✔", " ✔", "", "警卫哨", "--")
        
        tt.AddRow("", " ✔", "", "区域哨", "自由无人机审判场")
        tt.AddRow("", " ✔", "", "哨站", "不洁小站")
        tt.AddRow("", " ✔", "", "小型附属区", "废墟")
        tt.AddRow("", " ✔", "", "附属区", "--")
        
        tt.AddRow("", "", " ✔", "基地", "独立")
        tt.AddRow("", "", " ✔", "堡垒", "眩光")
        tt.AddRow("", "", " ✔", "军事基地", "等级制度")
        tt.AddRow("", "", " ✔", "辖区总部", "--")
        tt.AddRow("", "", " ✔", "黑暗血袭者舰队集结点", "--")

        using (cmdArg.OpenResponse(ForceImage)) (fun ret -> ret.Write(tt))