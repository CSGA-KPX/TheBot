module KPX.TheBot.Data.XivData.World

[<AutoOpen>]
module private WorldConstant =
    let WorldsStr =
        [| "一区", "晨曦王座,沃仙曦染,宇宙和音,红玉海,萌芽池,神意之地,幻影群岛,拉诺西亚"
           "二区", "拂晓之间,龙巢神殿,旅人栈桥,白金幻象,白银乡,神拳痕,潮风亭"
           "三区", "琥珀原,柔风海湾,海猫茶屋,延夏,静语庄园,摩杜纳,紫水栈桥" |]

    let WorldNamePinyin =
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

type World =
    { WorldId : uint16
      WorldName : string
      DataCenter : string }

let Worlds =
    let pyMapping = WorldNamePinyin |> readOnlyDict

    use col =
        KPX.TheBot.Data.Common.Database.BotDataInitializer.XivCollectionChs

    let worlds =
        col.GetSheet("World")
        |> Seq.map (fun row -> row.As<string>("Name"), uint16 row.Key.Main)
        |> readOnlyDict

    let output = System.Collections.Generic.List<World>()

    for dc, servers in WorldsStr do
        for server in servers.Split(",") do
            let id = worlds.[pyMapping.[server]]

            output.Add(
                { WorldId = id
                  WorldName = server
                  DataCenter = dc }
            )

    output.ToArray()

let WorldFromId =
    Worlds
    |> Array.map (fun x -> x.WorldId, x)
    |> readOnlyDict

let WorldFromName =
    Worlds
    |> Array.map (fun x -> x.WorldName, x)
    |> readOnlyDict

let DataCenterAlias =
    [| "一区", "一区,鸟区,陆行鸟区,鸟"
       "二区", "二区,猪区,莫古力区,猪"
       "三区", "三区,猫区,猫小胖区,猫" |]
    |> Seq.collect
        (fun (x, y) ->
            seq {
                for alias in y.Split(',') do
                    yield alias, x
            })
    |> readOnlyDict
