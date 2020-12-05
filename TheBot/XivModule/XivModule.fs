namespace TheBot.Module.XivModule

open System
open System.Text

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable
open KPX.FsCqHttp.Utils.UserOption


open BotData.XivData

open TheBot.Module.XivModule.Utils
open TheBot.Utils.Dicer

type XivModule() =
    inherit CommandHandlerBase()

    let itemCol = Item.ItemCollection.Instance

    let isNumber (str : string) =
        if str.Length <> 0 then String.forall (Char.IsDigit) str
        else false

    [<CommandHandlerMethodAttribute("幻想药", "洗个啥？", "")>]
    member x.HandleFantasia(msgArg : CommandArgs) = 
        let choices = 
            [|
                "屯着别用"
                "猫男"; "猫女"
                "龙男"; "龙女"
                "男精"; "女精"
                "公肥"; "母肥"
                "鲁加男"; "鲁加女"
                "大猫"; "兔子"
            |]
        let atUser = msgArg.MessageEvent.Message.GetAts() |> Array.tryHead
        if atUser.IsSome then
            match atUser.Value with
            | KPX.FsCqHttp.DataType.Message.AtUserType.All ->
                msgArg.AbortExecution(InputError, "全员发洗澡水？给我一瓶谢谢！")
            | KPX.FsCqHttp.DataType.Message.AtUserType.User i when i = msgArg.SelfId ->
                msgArg.AbortExecution(InputError, "请联系开发者")
            | KPX.FsCqHttp.DataType.Message.AtUserType.User _ ->
                msgArg.AbortExecution(ModuleError, "暂不支持")

        let dicer = new Dicer(SeedOption.SeedByUserDay(msgArg.MessageEvent))
        
        let tt = TextTable.FromHeader([|"1D100"; "选项"|])
        choices
        |> Array.map (fun str -> str, dicer.GetRandom(100u, str))
        |> Array.sortBy (snd)
        |> Array.iter (fun (str, d) -> tt.AddRow(d, str))

        msgArg.QuickMessageReply(tt.ToString())
        

    [<CommandHandlerMethodAttribute("cgss", "查找指定职业和品级的套装", "职业 品级")>]
    member x.HandleCGSS(msgArg : CommandArgs) =
        let mutable job = None
        let mutable iLv = None

        let cjm = ClassJobMapping.ClassJobMappingCollection.Instance
        for item in msgArg.Arguments do 
            if isNumber(item) then
                iLv <- Some(Int32.Parse(item))
            else
                let ret = cjm.TrySearchByName(item.ToUpperInvariant())
                if ret.IsSome then job <- Some (ret.Value.Value)
        if job.IsNone || iLv.IsNone then
            msgArg.AbortExecution(InputError, "请提供职业和品级信息。职业可以使用：单字简称/全程/英文简称")

        let cgc = CraftGearSet.CraftableGearCollection.Instance
        let ret = 
            cgc.Search(iLv.Value, job.Value)
            |> Array.map (fun g ->
                let item = Item.ItemCollection.Instance.GetByItemId(g.Id)
                if item.Name.Contains(" ") then
                    sprintf "#%i" item.Id
                else
                    item.Name
            )
        if ret.Length <> 0 then
            msgArg.QuickMessageReply(String.Join("+", ret))
        else
            msgArg.QuickMessageReply("没找到")

    [<CommandHandlerMethodAttribute("is", "（FF14）查找名字包含字符的物品", "关键词（大小写敏感）")>]
    member x.HandleItemSearch(msgArg : CommandArgs) =
        let att = AutoTextTable<Item.ItemRecord>(
                    [|
                        "Id", fun (item : Item.ItemRecord) -> box(item.Id.ToString())
                        "物品", fun item -> box(item.Name)
                    |])
        let i = String.Join(" ", msgArg.Arguments)
        if isNumber (i) then
            let ret = itemCol.TryGetByItemId(i |> int32)
            if ret.IsSome then 
                att.AddObject(ret.Value)
        else
            let ret = itemCol.SearchByName(i) |> Array.sortBy (fun x -> x.Id)

            if ret.Length >= 50 then 
                msgArg.AbortExecution(InputError, "结果太多，请优化关键词")

            if ret.Length = 0 then
                att.AddRow("无", "无")
            else
                for item in ret do
                    att.AddObject(item)

        using (msgArg.OpenResponse()) (fun r -> r.Write(att))

    [<CommandHandlerMethodAttribute("gate", "挖宝选门", "")>]
    member x.HandleGate(msgArg : CommandArgs) =
        let tt = TextTable.FromHeader([|"1D100"; "门"|])

        [|"左"; "中"; "右"|]
        |> Array.map (fun door -> door, Dicer.RandomDicer.GetRandom(100u, door))
        |> Array.sortBy (snd)
        |> Array.iter (fun (door, score) -> tt.AddRow((sprintf "%03i" score), door))

        msgArg.QuickMessageReply(tt.ToString())

    [<CommandHandlerMethodAttribute("仙人彩", "仙人彩周常", "")>]
    member x.HandleCactpot(msgArg : CommandArgs) =
        let nums = 
            Seq.initInfinite (fun _ -> sprintf "%04i" (Dicer.RandomDicer.GetRandom(10000u) - 1))
            |> Seq.distinctBy (fun numStr -> numStr.[3])
            |> Seq.take 3
        msgArg.QuickMessageReply(sprintf "%s" (String.Join(" ", nums)))

    [<CommandHandlerMethodAttribute("nuannuan", "暖暖", "")>]
    [<CommandHandlerMethodAttribute("nrnr", "暖暖", "")>]
    member x.HandleNrnr(msgArg : CommandArgs) = 
        let handler = new Net.Http.HttpClientHandler()
        handler.AutomaticDecompression <- Net.DecompressionMethods.GZip

        use hc = new Net.Http.HttpClient(handler)
        hc.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:78.0) Gecko/20100101 Firefox/78.0")
        hc.DefaultRequestHeaders.Referrer <- new Uri("https://space.bilibili.com/15503317/channel/detail?cid=55877")

        let json = hc
                    .GetStreamAsync("https://api.bilibili.com/x/space/channel/video?mid=15503317&cid=55877&pn=1&ps=30&order=0")
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult()
        use reader = new Newtonsoft.Json.JsonTextReader(new IO.StreamReader(json))
        let json = Newtonsoft.Json.Linq.JObject.Load(reader)
        let data = json.SelectToken("data.list.archives") :?> Newtonsoft.Json.Linq.JArray
        let item = data
                   |> Seq.find (fun x -> x.Value<string>("title").Contains("满分攻略"))
        let bvid = item.Value<string>("bvid")

        let url = sprintf "https://www.bilibili.com/video/%s" bvid

        let html = hc
                    .GetStreamAsync(url)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult()


        let doc = HtmlAgilityPack.HtmlDocument()

        doc.Load(html, Encoding.GetEncoding("UTF-8"))
        let n = doc.DocumentNode.SelectSingleNode("//div[@id = \"v_desc\"]")

        let cfg = Utils.CommandUtils.XivConfig(msgArg)
        using (msgArg.OpenResponse(cfg.IsImageOutput)) (fun ret -> ret.Write(n.InnerText))

    [<CommandHandlerMethodAttribute("海钓", "FF14海钓攻略", "next:查阅n个CD后的信息，list:查阅n个时间窗的信息")>]
    member x.HandleOceanFishing(msgArg : CommandArgs) = 
        let cfg = UserOptionParser()
        cfg.RegisterOption("next", "0")
        cfg.RegisterOption("list", "12")
        cfg.Parse(msgArg.Arguments)

        let dateFmt = "yyyy/MM/dd HH:00"

        use ret = msgArg.OpenResponse(ForceImage)
        ret.WriteLine("警告：国服数据，世界服不一定适用。时间为中国标准时间。")
        let mutable now = GetCstTime()
        try
            if cfg.IsDefined("list") then
                let tt = TextTable.FromHeader([|"CD时间"; "概述"; "tid"; "rid"|])
                let count = cfg.GetValue<int>("list")
                if count > 12*31 then msgArg.AbortExecution(InputError,  "那时间可太长了。")
                for i = 0 to count - 1 do 
                    let cd = now.AddHours((float i) * 2.0)
                    let info = OceanFishing.CalculateCooldown(cd)
                    tt.AddRow(info.CooldownDate.ToString(dateFmt), info.Message.[0], info.RouTableId, info.RouteId)
                ret.Write(tt)
            else
                let next= cfg.GetValue<int>("next")
                if next <> 0 then now <- now.AddHours(2.0 * (float next))
                let info = OceanFishing.CalculateCooldown(now)

                let date = info.CooldownDate.ToString(dateFmt)
                ret.WriteLine("预计CD时间为：{0}", date)
                ret.WriteEmptyLine()
                ret.WriteLine("攻略文本：")
            
                for line in info.Message do 
                    if String.IsNullOrWhiteSpace(line) then 
                        ret.WriteEmptyLine() else ret.WriteLine(line)

                ret.WriteEmptyLine()
                ret.WriteLine("数据源：https://bbs.nga.cn/read.php?tid=20553241")
                ret.WriteLine("调试信息:IKDRouteTable:{0}, IKDRoute:{1}, isNext:{2}", info.RouTableId, info.RouteId, info.IsNextCooldown)
        with
        | e -> ret.AbortExecution(ModuleError, "CD计算错误，请通告管理员：\r\n{0}", e)