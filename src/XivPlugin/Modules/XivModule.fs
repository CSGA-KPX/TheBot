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
            let c = XivContentCollection.Instance.GetDailyFrontline(VersionRegion.China)
            let o = XivContentCollection.Instance.GetDailyFrontline(VersionRegion.Offical)

            AsCols [ Literal "中国时间"
                     Literal "国服"
                     Literal "世界服" ]

            AsCols [ Literal "当前"
                     Literal $"%s{getString (startDate, c)}"
                     Literal $"%s{getString (startDate, o)}" ]

            [ for i = 1 to listCount do
                  let date = startDate.AddDays(float i)

                  let dateStr =
                      startDate
                          .AddDays(Operators.float i)
                          .ToString(dateFmt)

                  [ Literal dateStr; Literal(getString (date, c)); Literal(getString (date, o)) ] ]
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

        let dicer = Dicer(DiceSeed.SeedByUserDay(cmdArg.MessageEvent))

        TextTable(ForceText) {
            AsCols [ Literal "D100"; Literal "选项" ]

            choices
            |> Seq.map (fun str -> str, dicer.GetPositive(100u, str))
            |> Seq.sortBy snd
            |> Seq.map (fun (str, d) -> [ Integer(int d); Literal str ])
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
            AsCols [ Literal "D100"; Literal "门" ]

            [| "左"; "中"; "右" |]
            |> Array.map (fun door -> door, Dicer.RandomDicer.GetPositive(100u, door))
            |> Array.sortBy snd
            |> Array.map (fun (door, score) -> [ Literal $"%03i{score}"; Literal door ])
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
    [<CommandHandlerMethod("#nrnr", "FF14暖暖查询", "", IsHidden = true)>]
    member x.HandleNrnr(cmdArg: CommandEventArgs) =
        let handler = new Net.Http.HttpClientHandler()
        handler.AutomaticDecompression <- Net.DecompressionMethods.GZip

        use hc = new Net.Http.HttpClient(handler)

        hc.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:104.0) Gecko/20100101 Firefox/104.0"
        )

        hc.DefaultRequestHeaders.Referrer <- Uri("https://www.bilibili.com/video/BV1LV4y1j7xn/")

        let json =
            hc
                .GetStringAsync(
                    "https://api.bilibili.com/x/series/archives?mid=15503317&series_id=237700&only_normal=true&sort=desc&pn=1&ps=30"
                )
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()

        let json = Newtonsoft.Json.Linq.JObject.Parse(json)

        let data = json.SelectToken("data.archives") :?> Newtonsoft.Json.Linq.JArray

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
            Literal title
            LeftPad
            SplitTextRows n.InnerText
        }

    [<TestFixture>]
    member x.TestNrnr() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#nrnr")
        tc.ShouldNotThrow("#nuannuan")

    [<CommandHandlerMethod("#海钓", "", "", IsHidden = true)>]
    member x.HandleOceanFishing(cmdArg: CommandEventArgs) = cmdArg.Reply("指令已不再维护")
