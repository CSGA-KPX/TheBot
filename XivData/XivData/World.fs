module BotData.XivData.World

type World =
    { WorldId : uint16
      WorldName : string
      DataCenter: string}

let Worlds =
    // 一区 晨曦王座,沃仙曦染,宇宙和音,红玉海,萌芽池,神意之地,幻影群岛,拉诺西亚
    // 二区 拂晓之间,龙巢神殿,旅人栈桥,白金幻象,白银乡,神拳痕,潮风亭
    // 三区 琥珀原,柔风海湾,海猫茶屋,延夏,静语庄园,摩杜纳,紫水栈桥
    [|
        1175us, "晨曦王座", "一区"
        1174us, "沃仙曦染", "一区"
        1173us, "宇宙和音", "一区"
        1167us, "红玉海", "一区"
        1060us, "萌芽池", "一区"
        1081us, "神意之地", "一区"
        1044us, "幻影群岛", "一区"
        1042us, "拉诺西亚", "一区"

        1121us, "拂晓之间", "二区"
        1166us, "龙巢神殿", "二区"
        1113us, "旅人栈桥", "二区"
        1076us, "白金幻象", "二区"
        1172us, "白银乡", "二区"
        1171us, "神拳痕", "二区"
        1070us, "潮风亭", "二区"

        1179us, "琥珀原", "三区"
        1178us, "柔风海湾", "三区"
        1177us, "海猫茶屋", "三区"
        1169us, "延夏", "三区"
        1106us, "静语庄园", "三区"
        1045us, "摩杜纳", "三区"
        1043us, "紫水栈桥", "三区"
    |]
    |> Array.map (fun (id, n, dc) ->
        { WorldId = id
          WorldName = n 
          DataCenter = dc })

let DataCenterAlias = 
    [|
        "一区", "一区,鸟区,陆行鸟区"
        "二区", "二区,猪区,莫古力区"
        "三区", "三区,猫区,猫小胖区"
    |]
    |> Seq.collect (fun (x,y) ->
        seq {for alias in y.Split(',') do yield alias,x })
    |> readOnlyDict
    
let WorldFromId =
    Worlds
    |> Array.map (fun x -> x.WorldId, x)
    |> readOnlyDict

let WorldFromName =
    Worlds
    |> Array.map (fun x -> x.WorldName, x)
    |> readOnlyDict
