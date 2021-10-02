namespace KPX.TheBot.Module.EveModule

open System
open System.Collections.Generic

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Testing
open KPX.FsCqHttp.Utils.TextResponse

open KPX.TheBot.Data.EveData
open KPX.TheBot.Utils.EmbeddedResource

open KPX.TheBot.Module.EveModule.Utils.Config


type CombatSiteInfo =
    { Type : string
      FoundIn : string
      Difficulty : string
      Name : string }

type EveMiscModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethod("#evehelp", "EVE星系成本指数查询", "")>]
    member x.HandleEvehelp(_ : CommandEventArgs) =
        let cfg = EveConfigParser()
        cfg.Parse(Array.empty)

        TextTable() {
            [ CellBuilder() { literal "名称" }
              CellBuilder() { literal "意义" }
              CellBuilder() { literal "默认" } ]

            [ CellBuilder() { literal "ime" }
              CellBuilder() { literal "输入蓝图材料效率" }
              CellBuilder() { literal cfg.InputMe } ]

            [ CellBuilder() { literal "dme" }
              CellBuilder() { literal "衍生蓝图材料效率" }
              CellBuilder() { literal cfg.DerivationMe } ]

            [ CellBuilder() { literal "sci" }
              CellBuilder() { literal "星系成本指数" }
              CellBuilder() { literal cfg.SystemCostIndex } ]

            [ CellBuilder() { literal "tax" }
              CellBuilder() { literal "设施税率" }
              CellBuilder() { literal cfg.StructureTax } ]

            [ CellBuilder() { literal "p" }
              CellBuilder() { literal "设置后展开行星材料" }
              CellBuilder() { literal cfg.ExpandPlanet } ]

            [ CellBuilder() { literal "r" }
              CellBuilder() { literal "设置后展开反应材料" }
              CellBuilder() { literal cfg.ExpandReaction } ]

            [ CellBuilder() { literal "buy" }
              CellBuilder() { literal "设置后使用求购价格" }
              CellBuilder() { literal cfg.MaterialPriceMode } ]

            [ CellBuilder() { literal "text" }
              CellBuilder() { literal "设置后使用文本输出（部分指令不支持）" }
              CellBuilder() { literal (cfg.IsDefined("text")) } ]


        }

    [<TestFixture>]
    member x.TestEveHelp() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#evehelp")

    [<CommandHandlerMethod("#evesci", "EVE星系成本指数查询", "")>]
    member x.HandleSci(cmdArg : CommandEventArgs) =
        let sc =
            SolarSystems.SolarSystemCollection.Instance

        let scc =
            SystemCostIndexCache.SystemCostIndexCollection.Instance

        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.HeaderArgs)


        TextTable() {
            [ CellBuilder() { literal "星系" }
              CellBuilder() { literal "制造" }
              CellBuilder() { literal "材料" }
              CellBuilder() { literal "事件" }
              CellBuilder() { literal "拷贝" }
              CellBuilder() { literal "发明" }
              CellBuilder() { literal "反应" } ]

            [ for arg in cfg.NonOptionStrings do
                  let sys = sc.TryGetBySolarSystem(arg)

                  if sys.IsNone then
                      cmdArg.Abort(InputError, $"%s{arg}不是有效星系名称")

                  let sci = scc.GetBySystem(sys.Value)

                  [ CellBuilder() { literal arg }
                    CellBuilder() { percent sci.Manufacturing }
                    CellBuilder() { percent sci.ResearchMaterial }
                    CellBuilder() { percent sci.ResearchTime }
                    CellBuilder() { percent sci.Copying }
                    CellBuilder() { percent sci.Invention }
                    CellBuilder() { percent sci.Reaction } ] ]
        }

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

    [<CommandHandlerMethod("#eve异常", "EVE异常/死亡表 可接信号名称", "")>]
    [<CommandHandlerMethod("#eve死亡", "EVE异常/死亡表 可接信号名称", "")>]
    member x.HandleUnrated(cmdArg : CommandEventArgs) =
        if cmdArg.HeaderArgs.Length = 0 then
            let mgr =
                StringResource("EVE").GetLines("战斗空间信号")
                |> Array.map (fun line -> line.Split('\t'))
            use ret = cmdArg.OpenResponse(ForceImage
                                          )
            ret.Table {
                [ for line in mgr do
                      line
                      |> Array.map (fun str -> CellBuilder() { literal str }) ]
            }
            |> ignore
        else
            use ret = cmdArg.OpenResponse(ForceText)

            let names = HashSet<string>()

            for line in cmdArg.AllLines do
                let split =
                    line.Split('\t', StringSplitOptions.None)

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

                if not found then ret.WriteLine("{0}：未找到相关信息", arg)
