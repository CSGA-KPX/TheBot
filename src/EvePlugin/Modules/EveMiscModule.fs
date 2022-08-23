namespace KPX.EvePlugin.Modules

open System
open System.Collections.Generic

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Testing
open KPX.FsCqHttp.Utils.TextResponse

open KPX.TheBot.Host.Data

open KPX.EvePlugin.Data
open KPX.EvePlugin.Utils.Config


type CombatSiteInfo =
    { Type: string
      FoundIn: string
      Difficulty: string
      Name: string }

type EveMiscModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethod("#evehelp", "EVE配方计算参数文档", "")>]
    member x.HandleEvehelp(_: CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(Array.empty)

        TextTable() {
            AsCols [ Literal "名称"
                     Literal "意义"
                     Literal "默认" ]

            AsCols [ Literal "ime"
                     Literal "输入蓝图材料效率"
                     Literal cfg.InputMe ]

            AsCols [ Literal "dme"
                     Literal "衍生蓝图材料效率"
                     Literal cfg.DerivationMe ]

            AsCols [ Literal "sci"
                     Literal "星系成本指数"
                     Literal cfg.SystemCostIndex ]

            AsCols [ Literal "tax"
                     Literal "设施税率"
                     Literal cfg.StructureTax ]

            AsCols [ Literal "p"
                     Literal "设置后展开行星材料"
                     Literal(cfg.ExpandPlanet.ToString()) ]

            AsCols [ Literal "r"
                     Literal "设置后展开反应材料"
                     Literal(cfg.ExpandReaction.ToString()) ]

            AsCols [ Literal "buy"
                     Literal "设置后使用求购价格"
                     Literal(cfg.MaterialPriceMode.ToString()) ]

            AsCols [ Literal "text"
                     Literal "设置后使用文本输出（部分指令不支持）"
                     Literal(cfg.IsDefined("text").ToString()) ]
        }

    [<TestFixture>]
    member x.TestEveHelp() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#evehelp")

    [<CommandHandlerMethod("#evesci", "EVE星系成本指数查询", "")>]
    member x.HandleSci(cmdArg: CommandEventArgs) =
        let sc = SolarSystems.SolarSystemCollection.Instance

        let scc = SystemCostIndexCache.SystemCostIndexCollection.Instance

        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.HeaderArgs)


        TextTable() {
            AsCols [ Literal "星系"
                     Literal "制造"
                     Literal "材料"
                     Literal "时间"
                     Literal "拷贝"
                     Literal "发明"
                     Literal "反应" ]

            [ for arg in cfg.NonOptionStrings do
                  let sys = sc.TryGetBySolarSystem(arg)

                  if sys.IsNone then
                      cmdArg.Abort(InputError, $"%s{arg}不是有效星系名称")

                  let sci = scc.GetBySystem(sys.Value)

                  [ Literal arg
                    Percent sci.Manufacturing
                    Percent sci.ResearchMaterial
                    Percent sci.ResearchTime
                    Percent sci.Copying
                    Percent sci.Invention
                    Percent sci.Reaction ] ]
        }

    [<TestFixture>]
    member x.TestSystemCostIndex() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#evesci 吉他 皮尔米特")

    member val EveCombatSites =
        [| let mgr =
               ResxManager("EvePlugin.EVE").GetLines("战斗空间信号")
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

    [<CommandHandlerMethod("#eve异常", "EVE异常/死亡表 可接信号名称", "")>]
    [<CommandHandlerMethod("#eve死亡", "EVE异常/死亡表 可接信号名称", "")>]
    member x.HandleUnrated(cmdArg: CommandEventArgs) =
        if cmdArg.HeaderArgs.Length = 0 then
            let mgr =
                ResxManager("EvePlugin.EVE").GetLines("战斗空间信号")
                |> Array.map (fun line -> line.Split('\t'))

            use ret = cmdArg.OpenResponse(ForceImage)

            ret.Table {
                [ for line in mgr do
                      line |> Array.map Literal ]
            }
            |> ignore
        else
            use ret = cmdArg.OpenResponse(ForceText)

            let names = HashSet<string>()

            for line in cmdArg.AllLines do
                let split = line.Split('\t', StringSplitOptions.None)

                if split.Length = 1 then
                    names.Add(split.[0]) |> ignore
                elif split.Length = 6 then
                    // EVE扫描格式
                    // ID 类型 类型 名称 信号强度 距离
                    names.Add(split.[3]) |> ignore

            if names.Contains(String.Empty) then
                names.Remove(String.Empty) |> ignore

            for arg in names do
                let mutable found = false

                // 一个名称有多重匹配，不能用
                for site in x.EveCombatSites do
                    if site.Name = arg then
                        found <- true
                        ret.WriteLine("{0}：", arg)
                        ret.WriteLine("	类型：{0}", site.Type)
                        ret.WriteLine("	难度：{0}", site.Difficulty)
                        ret.WriteLine("	安等：{0}", site.FoundIn)

                if not found then
                    ret.WriteLine("{0}：未找到相关信息", arg)
