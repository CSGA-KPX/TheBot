namespace KPX.XivPlugin.Modules.MiscModule

open System
open System.Text

open KPX.FsCqHttp.Message

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Testing

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.UserOption

open KPX.XivPlugin.Data
open KPX.XivPlugin.Modules

open KPX.TheBot.Host.Utils.Dicer


type DfcOption() as x =
    inherit CommandOption()

    member val ListCount = OptionCellSimple<int>(x, "list", 7)

type SeaFishingOption() as x =
    inherit CommandOption()

    member val ListCount = OptionCellSimple<int>(x, "list", 7)

    member val NextCoolDown = OptionCellSimple<int>(x, "next", 0)

type MiscModule() =
    inherit CommandHandlerBase()

    let chsDfcRoulettes = lazy (XivContentCollection.Instance.GetDailyFrontline(VersionRegion.China))
    let officalDfcRoulettes = lazy (XivContentCollection.Instance.GetDailyFrontline(VersionRegion.Offical))

    [<CommandHandlerMethod("#纷争前线", "FF14:今日轮转查询", "")>]
    member x.HandleDailyFrontlineChallenge(cmdArg: CommandEventArgs) =
        let opt = DfcOption()
        opt.Parse(cmdArg.HeaderArgs)

        let listCount = 7
        let dateFmt = "yyyy/MM/dd HH:00"
        let JSTOffset = TimeSpan.FromHours(9.0)

        let getString (dt: DateTimeOffset, dfcRoulettes: XivContent []) =
            let dt = dt.ToOffset(JSTOffset)

            let utc = DateTimeOffset.UnixEpoch.ToOffset(JSTOffset)

            let index = ((dt - utc).Days + 2) % dfcRoulettes.Length

            dfcRoulettes.[index].Name

        let startDate =
            let jst = DateTimeOffset.Now.ToOffset(JSTOffset)

            (jst - jst.TimeOfDay)
                .ToOffset(TimeSpan.FromHours(8.0))

        use resp = cmdArg.OpenResponse(ForceImage)

        resp.Table {
            let c = chsDfcRoulettes.Value
            let o = officalDfcRoulettes.Value

            [ CellBuilder() { literal "中国时间" }
              CellBuilder() { literal "国服" }
              CellBuilder() { literal "世界服" } ]

            [ CellBuilder() { literal $"当前" }
              CellBuilder() { literal $"%s{getString (startDate, c)}" }
              CellBuilder() { literal $"%s{getString (startDate, o)}" } ]

            [ for i = 1 to listCount do
                  let date = startDate.AddDays(float i)

                  let dateStr =
                      startDate
                          .AddDays(Operators.float i)
                          .ToString(dateFmt)

                  [ CellBuilder() { literal dateStr }
                    CellBuilder() { literal (getString (date, c)) }
                    CellBuilder() { literal (getString (date, o)) } ] ]
        }
        |> ignore

    [<TestFixture>]
    member x.TestXivDFC() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#纷争前线")

    [<CommandHandlerMethod("#洗澡水", "", "", IsHidden = true)>]
    [<CommandHandlerMethod("#幻想药", "洗个啥？", "")>]
    [<CommandHandlerMethod("#FF14职业", "", "", IsHidden = true)>]
    [<CommandHandlerMethod("#FF14战斗职业", "", "")>]
    [<CommandHandlerMethod("#FF14生采职业", "", "")>]
    [<CommandHandlerMethod("#FF14生活职业", "", "", IsHidden = true)>]
    member x.HandleFantasia(cmdArg: CommandEventArgs) =
        let choices =
            match cmdArg.CommandAttrib.Command with
            | "#洗澡水"
            | "#幻想药" ->
                seq {
                    yield! Race.RaceCombinations
                    yield "屯着别用"
                }
            | "#FF14职业"
            | "#FF14战斗职业" -> ClassJob.BattleClassJobs |> Seq.map (fun x -> x.Name)
            | "#FF14生采职业"
            | "#FF14生活职业" -> ClassJob.CraftGatherJobs |> Seq.map (fun x -> x.Name)
            | _ -> cmdArg.Abort(ModuleError, $"模块{(nameof x.HandleFantasia)}指令匹配失败")

        let atUser = cmdArg.MessageEvent.Message.TryGetAt()

        if atUser.IsSome then
            cmdArg.Abort(InputError, "暂不支持@")
            // 以下部分暂时不会执行
            match atUser.Value with
            | AtUserType.All -> cmdArg.Abort(InputError, "非法操作")
            | AtUserType.User i when i = cmdArg.BotUserId -> cmdArg.Abort(InputError, "非法操作")
            | AtUserType.User _ -> cmdArg.Abort(ModuleError, "暂不支持")

        let dicer = Dicer(SeedOption.SeedByUserDay(cmdArg.MessageEvent))

        TextTable(ForceText) {
            [ CellBuilder() { literal "D100" }; CellBuilder() { literal "选项" } ]

            choices
            |> Seq.map (fun str -> str, dicer.GetPositive(100u, str))
            |> Seq.sortBy snd
            |> Seq.map (fun (str, d) -> [ CellBuilder() { integer d }; CellBuilder() { literal str } ])
        }

    [<TestFixture>]
    member x.TestXivFantasia() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#幻想药")

    (*
    [<CommandHandlerMethod("#is", "（FF14）查找名字包含字符的物品", "关键词（大小写敏感）")>]
    member x.HandleItemSearch(cmdArg : CommandEventArgs) =
        cmdArg.Reply("砍掉重练中")

    [<TestFixture>]
    member x.TestItemSearch() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#is 风之水晶")
        tc.ShouldThrow("#is 第三期")*)

    [<CommandHandlerMethod("#gate", "FF14:挖宝选门", "")>]
    member x.HandleGate(_: CommandEventArgs) =
        TextTable(ForceText) {
            [ CellBuilder() { literal "D100" }; CellBuilder() { literal "门" } ]

            [| "左"; "中"; "右" |]
            |> Array.map (fun door -> door, Dicer.RandomDicer.GetPositive(100u, door))
            |> Array.sortBy snd
            |> Array.map (fun (door, score) ->
                [ CellBuilder() { literal $"%03i{score}" }; CellBuilder() { literal door } ])
        }

    [<TestFixture>]
    member x.TestGate() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#gate")

    [<CommandHandlerMethod("#仙人彩", "仙人彩周常", "")>]
    member x.HandleCactpot(cmdArg: CommandEventArgs) =
        let nums =
            Seq.initInfinite (fun _ -> $"%04i{Dicer.RandomDicer.GetPositive(10000u) - 1u}")
            |> Seq.distinctBy (fun numStr -> numStr.[3])
            |> Seq.take 3

        cmdArg.Reply(sprintf "%s" (String.Join(" ", nums)))

    [<TestFixture>]
    member x.TestCactpot() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#仙人彩")

    [<CommandHandlerMethod("#nuannuan", "FF14暖暖查询", "")>]
    [<CommandHandlerMethod("#nrnr", "FF14暖暖查询", "")>]
    member x.HandleNrnr(cmdArg: CommandEventArgs) =
        let handler = new Net.Http.HttpClientHandler()
        handler.AutomaticDecompression <- Net.DecompressionMethods.GZip

        use hc = new Net.Http.HttpClient(handler)

        hc.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:78.0) Gecko/20100101 Firefox/78.0"
        )

        hc.DefaultRequestHeaders.Referrer <- Uri("https://space.bilibili.com/15503317/channel/detail?cid=55877")

        let json =
            hc
                .GetStreamAsync(
                    "https://api.bilibili.com/x/space/arc/search?mid=15503317&ps=30&tid=0&pn=1&keyword=&order=pubdate&jsonp=jsonp"
                )
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()

        use reader = new Newtonsoft.Json.JsonTextReader(new IO.StreamReader(json))

        let json = Newtonsoft.Json.Linq.JObject.Load(reader)

        let data = json.SelectToken("data.list.vlist") :?> Newtonsoft.Json.Linq.JArray

        let item = data |> Seq.find (fun x -> x.Value<string>("title").Contains("满分攻略"))

        let bvid = item.Value<string>("bvid")
        let title = item.Value<string>("title")

        let url = $"https://www.bilibili.com/video/%s{bvid}"

        let html =
            hc
                .GetStreamAsync(url)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()

        let doc = HtmlAgilityPack.HtmlDocument()

        doc.Load(html, Encoding.GetEncoding("UTF-8"))

        let n = doc.DocumentNode.SelectSingleNode("//div[@id = \"v_desc\"]")

        let cfg = Utils.CommandUtils.XivOption()
        cfg.Parse(cmdArg.HeaderArgs)

        TextTable(cfg.ResponseType) {
            CellBuilder() {
                literal title
                newLine
                splitString n.InnerText
            }
        }

    [<TestFixture>]
    member x.TestNrnr() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#nrnr")
        tc.ShouldNotThrow("#nuannuan")

    [<CommandHandlerMethod("#海钓",
                           "FF14海钓攻略",
                           "next:查阅n个CD后的信息，list:查阅n个时间窗的信息。如：
#海钓 next:2
#海钓 list:50")>]
    member x.HandleOceanFishing(cmdArg: CommandEventArgs) =
        let opt = SeaFishingOption()
        opt.Parse(cmdArg.HeaderArgs)

        let dateFmt = "yyyy/MM/dd HH:00"
        use ret = cmdArg.OpenResponse(ForceImage)
        ret.WriteLine("缺少国际服资料，目前仅查询国服攻略。时间为中国标准时间。")

        let mutable now = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(8.0))

        try
            if opt.ListCount.IsDefined then
                let count = opt.ListCount.Value

                if count > 12 * 31 then
                    cmdArg.Abort(InputError, "那时间可太长了。")

                ret.Table {
                    [ CellBuilder() { literal "CD时间" }
                      CellBuilder() { literal "概述" }
                      CellBuilder() { literal "tid" }
                      CellBuilder() { literal "rid" } ]

                    [ for i = 0 to count - 1 do
                          let cd = now.AddHours((float i) * 2.0)
                          let info = OceanFishing.CalculateCooldown(cd, VersionRegion.China)

                          [ CellBuilder() { dateTime info.CooldownDate }
                            CellBuilder() { literal info.Message.[0] }
                            CellBuilder() { literal info.RouTableId }
                            CellBuilder() { literal info.RouteId } ] ]
                }
                |> ignore
            else
                let next = opt.NextCoolDown.Value

                if next <> 0 then
                    now <- now.AddHours(2.0 * (float next))

                let info = OceanFishing.CalculateCooldown(now, VersionRegion.China)

                let date = info.CooldownDate.ToString(dateFmt)
                ret.WriteLine("预计CD时间为：{0}", date)
                ret.WriteLine()
                ret.WriteLine("攻略文本：")

                for line in info.Message do
                    if String.IsNullOrWhiteSpace(line) then
                        ret.WriteLine()
                    else
                        ret.WriteLine(line)

                ret.WriteLine()
                ret.WriteLine("数据源：https://bbs.nga.cn/read.php?tid=20553241")
                ret.WriteLine("        https://bbs.nga.cn/read.php?tid=26210039")
                ret.WriteLine("        https://bbs.nga.cn/read.php?tid=26218473")
                ret.WriteLine("        https://bbs.nga.cn/read.php?tid=25905000")

                ret.WriteLine(
                    "调试信息:IKDRouteTable:{0}, IKDRoute:{1}, isNext:{2}",
                    info.RouTableId,
                    info.RouteId,
                    info.IsNextCooldown
                )
        with
        | e -> ret.Abort(ModuleError, "CD计算错误，请通告管理员：\r\n{0}", e)

    [<TestFixture>]
    member x.TestIKD() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#海钓")
        tc.ShouldNotThrow("#海钓 next:1")
        tc.ShouldNotThrow("#海钓 next:10")
        tc.ShouldNotThrow("#海钓 list:10")
