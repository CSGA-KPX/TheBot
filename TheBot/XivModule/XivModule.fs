namespace KPX.TheBot.Module.XivModule

open System
open System.Text

open KPX.FsCqHttp.Message

open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable
open KPX.FsCqHttp.Utils.UserOption

open KPX.TheBot.Data.XivData

open KPX.TheBot.Utils.Dicer


type XivModule() =
    inherit CommandHandlerBase()

    let itemCol = Item.ItemCollection.Instance

    let isNumber (str : string) =
        if str.Length <> 0 then String.forall (Char.IsDigit) str else false

    let buildDfc () =
        ContentFinderCondition.XivContentCollection.Instance.GetAll()
        |> Seq.filter (fun i -> i.IsDailyFrontlineChallengeRoulette)
        |> Seq.toArray
        |> Array.sortBy (fun i -> i.Id)

    let mutable dfcRoulettes = buildDfc ()

    [<CommandHandlerMethodAttribute("纷争前线", "今日轮转查询", "")>]
    member x.HandleDailyFrontlineChallenge(cmdArg : CommandEventArgs) =
        let uo = UserOptionParser()
        uo.RegisterOption("list", "7")
        uo.RegisterOption("rebuild", "")
        uo.Parse(cmdArg.Arguments)

        if uo.IsDefined("rebuild") then
            dfcRoulettes <- buildDfc ()
            cmdArg.QuickMessageReply(sprintf "重建完成，当前有%i个副本" dfcRoulettes.Length)
        else
            if dfcRoulettes.Length = 0 then cmdArg.AbortExecution(ModuleError, "模块错误：副本表为空。请使用rebuild")

            let dateFmt = "yyyy/MM/dd HH:00"
            let JSTOffset = TimeSpan.FromHours(9.0)

            let getString (dt : DateTimeOffset) =
                let dt = dt.ToOffset(JSTOffset)

                let utc =
                    DateTimeOffset.UnixEpoch.ToOffset(JSTOffset)

                let index =
                    ((dt - utc).Days + 2) % dfcRoulettes.Length

                dfcRoulettes.[index].Name

            let tt =
                TextTable(RightAlignCell "日期（中国标准时间）", "副本")

            let startDate =
                let jst = DateTimeOffset.Now.ToOffset(JSTOffset)

                (jst - jst.TimeOfDay)
                    .ToOffset(TimeSpan.FromHours(8.0))

            tt.AddPreTable(sprintf "当前为：%s" (getString (startDate)))

            let list = uo.GetValue<int>("list")

            if list > 31 then cmdArg.AbortExecution(InputError, "一个月还不够嘛？")

            for i = 0 to uo.GetValue<int>("list") do
                let date = startDate.AddDays(float i)

                let dateStr =
                    startDate.AddDays(float i).ToString(dateFmt)

                let contentStr = getString (date)
                tt.AddRow(dateStr, contentStr)

            using (cmdArg.OpenResponse(ForceImage)) (fun ret -> ret.Write(tt))

    [<CommandHandlerMethodAttribute("幻想药", "洗个啥？", "")>]
    member x.HandleFantasia(cmdArg : CommandEventArgs) =
        let choices =
            [| "屯着别用"
               "猫男"
               "猫女"
               "龙男"
               "龙女"
               "男精"
               "女精"
               "公肥"
               "母肥"
               "鲁加男"
               "鲁加女"
               "大猫"
               "兔子" |]

        let atUser = cmdArg.MessageEvent.Message.TryGetAt()

        if atUser.IsSome then
            match atUser.Value with
            | AtUserType.All -> cmdArg.AbortExecution(InputError, "全员发洗澡水？给我一瓶谢谢！")
            | AtUserType.User i when i = cmdArg.BotUserId -> cmdArg.AbortExecution(InputError, "请联系开发者")
            | AtUserType.User _ -> cmdArg.AbortExecution(ModuleError, "暂不支持")

        let dicer =
            new Dicer(SeedOption.SeedByUserDay(cmdArg.MessageEvent))

        let tt = TextTable("1D100", "选项")

        choices
        |> Array.map (fun str -> str, dicer.GetRandom(100u, str))
        |> Array.sortBy (snd)
        |> Array.iter (fun (str, d) -> tt.AddRow(d, str))

        cmdArg.QuickMessageReply(tt.ToString())

    [<CommandHandlerMethodAttribute("cgss", "查找指定职业和品级的套装", "职业 品级")>]
    member x.HandleCGSS(cmdArg : CommandEventArgs) =
        let mutable job = None
        let mutable iLv = None

        let cjm =
            ClassJobMapping.ClassJobMappingCollection.Instance

        for item in cmdArg.Arguments do
            if isNumber (item) then
                iLv <- Some(Int32.Parse(item))
            else
                let ret =
                    cjm.TrySearchByName(item.ToUpperInvariant())

                if ret.IsSome then job <- Some(ret.Value.Value)

        if job.IsNone || iLv.IsNone
        then cmdArg.AbortExecution(InputError, "请提供职业和品级信息。职业可以使用：单字简称/全程/英文简称")

        let cgc =
            CraftGearSet.CraftableGearCollection.Instance

        let ret =
            cgc.Search(iLv.Value, job.Value)
            |> Array.map
                (fun g ->
                    let item =
                        Item.ItemCollection.Instance.GetByItemId(g.Id)

                    if item.Name.Contains(" ") then sprintf "#%i" item.Id else item.Name)

        if ret.Length <> 0 then cmdArg.QuickMessageReply(String.Join("+", ret)) else cmdArg.QuickMessageReply("没找到")

    [<CommandHandlerMethodAttribute("is", "（FF14）查找名字包含字符的物品", "关键词（大小写敏感）")>]
    member x.HandleItemSearch(cmdArg : CommandEventArgs) =
        let tt = TextTable(RightAlignCell "Id", "物品名")
        let i = String.Join(" ", cmdArg.Arguments)

        if isNumber (i) then
            let ret = itemCol.TryGetByItemId(i |> int32)
            if ret.IsSome then tt.AddRow(ret.Value.Id, ret.Value.Name)
        else
            let ret =
                itemCol.SearchByName(i)
                |> Array.sortBy (fun x -> x.Id)

            if ret.Length >= 50 then cmdArg.AbortExecution(InputError, "结果太多，请优化关键词")

            if ret.Length = 0 then cmdArg.AbortExecution(InputError, "无结果")

            for item in ret do
                tt.AddRow(item.Id, item.Name)

        using (cmdArg.OpenResponse()) (fun r -> r.Write(tt))

    [<CommandHandlerMethodAttribute("gate", "挖宝选门", "")>]
    member x.HandleGate(cmdArg : CommandEventArgs) =
        let tt = TextTable("1D100", "门")

        [| "左"; "中"; "右" |]
        |> Array.map (fun door -> door, Dicer.RandomDicer.GetRandom(100u, door))
        |> Array.sortBy (snd)
        |> Array.iter (fun (door, score) -> tt.AddRow((sprintf "%03i" score), door))

        cmdArg.QuickMessageReply(tt.ToString())

    [<CommandHandlerMethodAttribute("仙人彩", "仙人彩周常", "")>]
    member x.HandleCactpot(cmdArg : CommandEventArgs) =
        let nums =
            Seq.initInfinite (fun _ -> sprintf "%04i" (Dicer.RandomDicer.GetRandom(10000u) - 1))
            |> Seq.distinctBy (fun numStr -> numStr.[3])
            |> Seq.take 3

        cmdArg.QuickMessageReply(sprintf "%s" (String.Join(" ", nums)))

    [<CommandHandlerMethodAttribute("nuannuan", "暖暖", "")>]
    [<CommandHandlerMethodAttribute("nrnr", "暖暖", "")>]
    member x.HandleNrnr(cmdArg : CommandEventArgs) =
        let handler = new Net.Http.HttpClientHandler()
        handler.AutomaticDecompression <- Net.DecompressionMethods.GZip

        use hc = new Net.Http.HttpClient(handler)

        hc.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:78.0) Gecko/20100101 Firefox/78.0"
        )

        hc.DefaultRequestHeaders.Referrer <- new Uri("https://space.bilibili.com/15503317/channel/detail?cid=55877")

        let json =
            hc
                .GetStreamAsync("https://api.bilibili.com/x/space/channel/video?mid=15503317&cid=55877&pn=1&ps=30&order=0")
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()

        use reader =
            new Newtonsoft.Json.JsonTextReader(new IO.StreamReader(json))

        let json =
            Newtonsoft.Json.Linq.JObject.Load(reader)

        let data =
            json.SelectToken("data.list.archives") :?> Newtonsoft.Json.Linq.JArray

        let item =
            data
            |> Seq.find (fun x -> x.Value<string>("title").Contains("满分攻略"))

        let bvid = item.Value<string>("bvid")
        let title = item.Value<string>("title")

        let url =
            sprintf "https://www.bilibili.com/video/%s" bvid

        let html =
            hc
                .GetStreamAsync(url)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()

        let doc = HtmlAgilityPack.HtmlDocument()

        doc.Load(html, Encoding.GetEncoding("UTF-8"))

        let n =
            doc.DocumentNode.SelectSingleNode("//div[@id = \"v_desc\"]")

        let cfg = Utils.CommandUtils.XivConfig(cmdArg)

        using
            (cmdArg.OpenResponse(cfg.IsImageOutput))
            (fun ret ->
                ret.Write(title)
                ret.Write("\r\n")
                ret.Write(n.InnerText))

    [<CommandHandlerMethodAttribute("海钓", "FF14海钓攻略", "next:查阅n个CD后的信息，list:查阅n个时间窗的信息")>]
    member x.HandleOceanFishing(cmdArg : CommandEventArgs) =
        let cfg = UserOptionParser()
        cfg.RegisterOption("next", "0")
        cfg.RegisterOption("list", "12")
        cfg.Parse(cmdArg.Arguments)

        let dateFmt = "yyyy/MM/dd HH:00"

        use ret = cmdArg.OpenResponse(ForceImage)
        ret.WriteLine("警告：国服数据，世界服不一定适用。时间为中国标准时间。")
        let mutable now = GetCstTime()

        try
            if cfg.IsDefined("list") then
                let tt = TextTable("CD时间", "概述", "tid", "rid")
                let count = cfg.GetValue<int>("list")

                if count > 12 * 31 then cmdArg.AbortExecution(InputError, "那时间可太长了。")

                for i = 0 to count - 1 do
                    let cd = now.AddHours((float i) * 2.0)
                    let info = OceanFishing.CalculateCooldown(cd)

                    tt.AddRow(info.CooldownDate.ToString(dateFmt), info.Message.[0], info.RouTableId, info.RouteId)

                ret.Write(tt)
            else
                let next = cfg.GetValue<int>("next")
                if next <> 0 then now <- now.AddHours(2.0 * (float next))
                let info = OceanFishing.CalculateCooldown(now)

                let date = info.CooldownDate.ToString(dateFmt)
                ret.WriteLine("预计CD时间为：{0}", date)
                ret.WriteEmptyLine()
                ret.WriteLine("攻略文本：")

                for line in info.Message do
                    if String.IsNullOrWhiteSpace(line) then ret.WriteEmptyLine() else ret.WriteLine(line)

                ret.WriteEmptyLine()
                ret.WriteLine("数据源：https://bbs.nga.cn/read.php?tid=20553241")

                ret.WriteLine(
                    "调试信息:IKDRouteTable:{0}, IKDRoute:{1}, isNext:{2}",
                    info.RouTableId,
                    info.RouteId,
                    info.IsNextCooldown
                )
        with e -> ret.AbortExecution(ModuleError, "CD计算错误，请通告管理员：\r\n{0}", e)
