namespace KPX.TheBot.Data.XivData

open System
open System.Collections.Generic


type World =
    { WorldId : uint16
      mutable WorldName : string
      mutable DataCenter : string
      mutable IsPublic : bool }

module World =
    // 记录所有服务器
    let private idMapping = Dictionary<uint16, World>()

    let private nameMapping =
        Dictionary<string, World>(StringComparer.OrdinalIgnoreCase)

    let private dcNameMapping =
        Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

    let Worlds = idMapping.Values |> Seq.readonly

    let WorldNames = nameMapping.Keys |> Seq.readonly
    let DataCenterNames = dcNameMapping.Keys |> Seq.readonly


    let DefinedWorld (name : string) = nameMapping.ContainsKey(name)
    let DefinedDC (name : string) = dcNameMapping.ContainsKey(name)

    let GetDCByName (name : string) = dcNameMapping.[name]

    let GetWorldById (id : uint16) = idMapping.[id]
    let GetWorldByName (name : string) = nameMapping.[name]

    let GetWorldsByDC (dcName : string) =
        let mapped = dcNameMapping.[dcName]

        Worlds
        |> Seq.filter (fun w -> w.DataCenter = mapped && w.IsPublic)

    let private ChsWorldName =
        [| "拉诺西亚", "LaNuoXiYa"
           "紫水栈桥", "ZiShuiZhanQiao"
           "幻影群岛", "HuanYingQunDao"
           "摩杜纳", "MoDuNa"
           "萌芽池", "MengYaChi"
           "白金幻象", "BaiJinHuanXiang"
           "神意之地", "ShenYiZhiDi"
           "静语庄园", "JingYuZhuangYuan"
           "旅人栈桥", "LvRenZhanQiao"
           "拂晓之间", "FuXiaoZhiJian"
           "龙巢神殿", "Longchaoshendian"
           "红玉海", "HongYuHai"
           "延夏", "YanXia"
           "潮风亭", "ChaoFengTing"
           "神拳痕", "ShenQuanHen"
           "白银乡", "BaiYinXiang"
           "宇宙和音", "YuZhouHeYin"
           "沃仙曦染", "WoXianXiRan"
           "晨曦王座", "ChenXiWangZuo"
           "海猫茶屋", "HaiMaoChaWu"
           "柔风海湾", "RouFengHaiWan"
           "琥珀原", "HuPoYuan" |]

    let private ChsWorldsInfo =
        [| "一区", "晨曦王座,沃仙曦染,宇宙和音,红玉海,萌芽池,神意之地,幻影群岛,拉诺西亚"
           "二区", "拂晓之间,龙巢神殿,旅人栈桥,白金幻象,白银乡,神拳痕,潮风亭"
           "三区", "琥珀原,柔风海湾,海猫茶屋,延夏,静语庄园,摩杜纳,紫水栈桥" |]

    let private WorldNameAlias =
        [| "拉诺西亚", "拉诺"
           "静语庄园", "鲸鱼,静语"
           "海猫茶屋", "海猫"
           "紫水栈桥", "紫水"
           "白金幻象", "白金"
           "龙巢神殿", "龙巢"
           "旅人栈桥", "旅人"
           "拂晓之间", "拂晓"
           "神意之地", "神意"
           "幻影群岛", "幻影"

           "Aegis", "圣盾" // Elemental
           "Atomos", "阿托莫斯"
           "Carbuncle", "宝石兽"
           "Garuda", "迦楼罗"
           "Gungnir", "神枪,昆古尼尔,神枪昆古尼尔"
           "Kujata", "库加塔"
           "Ramuh", "拉姆"
           "Tonberry", "冬贝利"
           "Typhon", "提丰"
           "Unicorn", "独角兽"

           "Alexander", "亚历山大" //Gaia
           "Bahamut", "巴哈姆特"
           "Durandal", "圣剑，杜兰德尔，圣剑杜兰德尔"
           "Fenrir", "芬里尔"
           "Ifrit", "伊弗利特"
           "Ridill", "里德尔"
           "Tiamat", "提亚马特"
           "Ultima", "究极"
           "Valefor", "瓦利弗"
           "Yojimbo", "保镖"
           "Zeromus", "扎罗姆斯"

           "Anima", "元灵" //Mana
           "Asura", "阿修罗"
           "Belias", "贝利亚斯"
           "Chocobo", "陆行鸟"
           "Hades", "哈迪斯"
           "Ixion", "伊克西翁"
           "Mandragora", "蔓德拉"
           "Masamune", "正宗"
           "Pandaemonium", "伏魔殿"
           "Shinryu", "神龙"
           "Titan", "泰坦"

           "Adamantoise", "精金龟" //Aether
           "Cactuar", "仙人掌"
           "Faerie", "仙子"
           "Gilgamesh", "吉尔伽美什"
           "Jenova", "杰诺瓦"
           "Midgardsormr", "尘世幻龙"
           "Sargatanas", "撒伽塔纳斯"
           "Siren", "塞壬"

           "Behemoth", "贝希摩斯" //Primal
           "Excalibur", "王者之剑"
           "Exodus", "埃克斯狄斯"
           "Famfrit", "法姆弗里特"
           "Hyperion", "亥伯龙"
           "Lamia", "拉米亚"
           "Leviathan", "利维亚桑"
           "Ultros", "奥尔特罗斯"

           "Balmung", "巴鲁姆克" //Crystal
           "Brynhildr", "布伦希尔德"
           "Coeurl", "长须豹"
           "Diabolos", "迪亚波罗斯"
           "Goblin", "哥布林"
           "Malboro", "魔界花"
           "Mateus", "马提乌斯"
           "Zalera", "扎尔艾拉"

           "Cerberus", "刻耳柏洛斯" //Chaos
           "Louisoix", "路易索瓦"
           "Moogle", "混沌莫古力，莫古力服"
           "Omega", "欧米茄"
           "Ragnarok", "诸神黄昏"
           "Spriggan", "魔石精"

           "Lich", "巫妖" //Light
           "Odin", "奥丁"
           "Phoenix", "不死鸟,凤凰"
           "Shiva", "希瓦"
           "Twintania", "双塔尼亚"
           "Zodiark", "佐迪亚克" |]

    let private DcNameAlias =
        [| "一区", "鸟区,陆行鸟区,鸟"
           "二区", "猪区,莫古力区,猪"
           "三区", "猫区,猫小胖区,猫"
           "Elemental", "元素"
           "Gaia", "盖亚,盖娅"
           "Mana", "魔力,玛那,玛娜"
           "Aether", "以太"
           "Primal", "蛮神"
           "Crystal", "水晶"
           "Chaos", "混沌"
           "Light", "光" |]

    do
        use col =
            KPX.TheBot.Data.Common.Database.BotDataInitializer.XivCollectionChs

        for row in col.GetSheet("World") do
            let id = row.Key.Main |> uint16
            let name = row.As<string>("Name")
            let isPublic = row.As<bool>("IsPublic")

            let dc =
                row.AsRow("DataCenter").As<string>("Name")

            let world =
                { WorldId = id
                  WorldName = name
                  DataCenter = dc
                  IsPublic = isPublic }

            idMapping.Add(id, world)
            if not <| nameMapping.TryAdd(name, world) then printfn "World : 服务器添加失败 %A" world

        // 添加国服服务器名称
        for (chs, eng) in ChsWorldName do
            let w = GetWorldByName(eng)
            nameMapping.Add(chs, w)

        // 重写国服服务器的大区信息
        for (dcName, worlds) in ChsWorldsInfo do
            for world in worlds.Split(",") do
                GetWorldByName(world).DataCenter <- dcName
                GetWorldByName(world).IsPublic <- true

        // 添加服务器别名
        for (world, aliases) in WorldNameAlias do
            for alias in aliases.Split(",") do
                let isEmpty = String.IsNullOrWhiteSpace(alias)

                if not isEmpty then
                    nameMapping.TryAdd(alias, GetWorldByName(world))
                    |> ignore

        // 国际服的大区
        for row in col.GetSheet("WorldDCGroupType") do
            let name = row.As<string>("Name")

            if row.As<int>("Region") <> 0 then
                // INVALID = 0, BETA = 0
                dcNameMapping.Add(name, name)

        // 大区别名，因为国服自带了所以都有
        for (dc, aliases) in DcNameAlias do
            dcNameMapping.TryAdd(dc, dc) |> ignore

            for alias in aliases.Split(",") do
                dcNameMapping.TryAdd(alias, dc) |> ignore
