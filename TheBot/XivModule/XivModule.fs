namespace TheBot.Module.XivModule

open System
open KPX.FsCqHttp.Handler.CommandHandlerBase
open XivData
open TheBot.Module.XivModule.Utils
open TheBot.Utils.Dicer
open TheBot.Utils.TextTable

type XivModule() =
    inherit CommandHandlerBase()

    let itemCol = Item.ItemCollection.Instance

    let isNumber (str : string) =
        if str.Length <> 0 then String.forall (Char.IsDigit) str
        else false

    [<CommandHandlerMethodAttribute("cgss", "查找指定职业和品级的套装", "职业 品级")>]
    member x.HandleCGSS(msgArg : CommandArgs) =
        let mutable job = None
        let mutable iLv = None

        let cjm = XivData.ClassJobMapping.ClassJobMappingCollection.Instance
        for item in msgArg.Arguments do 
            if isNumber(item) then
                iLv <- Some(Int32.Parse(item))
            else
                let ret = cjm.TryLookupById(item.ToUpperInvariant())
                if ret.IsSome then job <- Some (ret.Value.Value)
        if job.IsNone || iLv.IsNone then
            failwithf "请提供职业和品级信息。职业可以使用：单字简称/全程/英文简称"

        let cgc = XivData.CraftGearSet.CraftableGearCollection.Instance
        let ret = 
            cgc.Search(iLv.Value, job.Value)
            |> Array.map (fun g ->
                let item = XivData.Item.ItemCollection.Instance.LookupById(g.Id)
                if item.Name.Contains(" ") then
                    sprintf "#%i" item.Id
                else
                    item.Name
            )
        msgArg.QuickMessageReply(String.Join("+", ret))

    [<CommandHandlerMethodAttribute("is", "查找名字包含字符的物品", "关键词（大小写敏感）")>]
    member x.HandleItemSearch(msgArg : CommandArgs) =
        let att = AutoTextTable<Item.ItemRecord>(
                    [|
                        "Id", fun item -> box(item.Id.ToString())
                        "物品", fun item -> box(item.Name)
                    |])
        let i = String.Join(" ", msgArg.Arguments)
        if isNumber (i) then
            let ret = itemCol.TryLookupById(i |> int32)
            if ret.IsSome then 
                att.AddObject(ret.Value)
        else
            let ret = itemCol.SearchByName(i) |> Array.sortBy (fun x -> x.Id)
            if ret.Length = 0 then
                att.AddRow("无", "无")
            else
                for item in ret do
                    att.AddObject(item)

        using (msgArg.OpenResponse()) (fun r -> r.Write(att))

    [<CommandHandlerMethodAttribute("gate", "挖宝选门", "")>]
    member x.HandleGate(msgArg : CommandArgs) =
        let dicer = Dicer(SeedRandom |> Array.singleton)
        let tt = TextTable.FromHeader([|"1D100"; "门"|])

        [|"左"; "中"; "右"|]
        |> Array.map (fun door -> door, dicer.GetRandomFromString(door, 100u))
        |> Array.sortBy (snd)
        |> Array.iter (fun (door, score) -> tt.AddRow((sprintf "%03i" score), door))

        msgArg.QuickMessageReply(tt.ToString())

    [<CommandHandlerMethodAttribute("仙人彩", "仙人彩周常", "")>]
    member x.HandleCactpot(msgArg : CommandArgs) =
        let dicer = Dicer(SeedRandom)
        let nums = 
            Seq.initInfinite (fun _ -> sprintf "%04i" (dicer.GetRandom(10000u) - 1))
            |> Seq.distinctBy (fun numStr -> numStr.[3])
            |> Seq.take 3
        msgArg.QuickMessageReply(sprintf "%s" (String.Join(" ", nums)))

    [<CommandHandlerMethodAttribute("mentor", "今日导随运势", "")>]
    member x.HandleMentor(msgArg : CommandArgs) =
        let sw = new IO.StringWriter()
        let dicer = Dicer(SeedOption.SeedByUserDay(msgArg.MessageEvent))

        let fortune, events =
            match dicer.GetRandom(100u) with
            | x when x <= 5 -> MentorUtils.fortune.[0]
            | x when x <= 20 -> MentorUtils.fortune.[1]
            | x when x <= 80 -> MentorUtils.fortune.[2]
            | x when x <= 95 -> MentorUtils.fortune.[3]
            | _ -> MentorUtils.fortune.[4]

        let event = dicer.GetRandomItem(events)
        sw.WriteLine("{0} 今日导随运势为：", msgArg.MessageEvent.GetNicknameOrCard)
        sw.WriteLine("{0} : {1}", fortune, event)

        let s, a =
            let count = MentorUtils.shouldOrAvoid.Count() |> uint32
            let idx = dicer.GetRandomArray(count + 1u, 3 * 2)
            let a = idx |> Array.map (fun id -> MentorUtils.shouldOrAvoid.[id].Value.Value)
            a.[0..2], a.[3..]
        sw.WriteLine("宜：{0}", String.concat "/" s)
        sw.WriteLine("忌：{0}", String.concat "/" a)
        let c, jobs = dicer.GetRandomItem(MentorUtils.classJob)
        let job = dicer.GetRandomItem(jobs)
        sw.WriteLine("推荐职业: {0} {1}", c, job)
        let location =
            let count = MentorUtils.location.Count() |> uint32
            let idx = dicer.GetRandom(count + 1u)
            MentorUtils.location.[idx].Value
        sw.WriteLine("推荐排本场所: {0}", location.Value)
        msgArg.QuickMessageReply(sw.ToString())