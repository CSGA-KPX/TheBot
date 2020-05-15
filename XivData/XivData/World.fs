module XivData.World

type World =
    { WorldId : uint16
      WorldName : string }

let Worlds =
    // 一区 晨曦王座,沃仙曦染,宇宙和音,红玉海,萌芽池,神意之地,幻影群岛,拉诺西亚
    // 二区 拂晓之间,龙巢神殿,旅人栈桥,白金幻象,白银乡,神拳痕,潮风亭
    // 三区 琥珀原,柔风海湾,海猫茶屋,延夏,静语庄园,摩杜纳,紫水栈桥
    [|
        //一区
        1175us, "晨曦王座"
        1174us, "沃仙曦染"
        1173us, "宇宙和音"
        1167us, "红玉海"
        1060us, "萌芽池"
        1081us, "神意之地"
        1044us, "幻影群岛"
        1042us, "拉诺西亚"

        //二区
        1121us, "拂晓之间"
        1166us, "龙巢神殿"
        1113us, "旅人栈桥"
        1076us, "白金幻象"
        1172us, "白银乡"
        1171us, "神拳痕"
        1070us, "潮风亭"

        //三区
        1179us, "琥珀原"
        1178us, "柔风海湾"
        1177us, "海猫茶屋"
        1169us, "延夏"
        1106us, "静语庄园"
        1045us, "摩杜纳"
        1043us, "紫水栈桥"
    |]
    |> Array.map (fun (id, n) ->
        { WorldId = id
          WorldName = n })

let DataCenters = 
    [|
        "一鸟", "晨曦王座,沃仙曦染,宇宙和音,红玉海,萌芽池,神意之地,幻影群岛,拉诺西亚"
        "二猪", "拂晓之间,龙巢神殿,旅人栈桥,白金幻象,白银乡,神拳痕,潮风亭"
        "三猫", "琥珀原,柔风海湾,海猫茶屋,延夏,静语庄园,摩杜纳,紫水栈桥"
    |]
    |> Array.map (fun (dc,ws) -> dc, ws.Split(','))
    

let WorldFromId =
    Worlds
    |> Array.map (fun x -> x.WorldId, x)
    |> readOnlyDict

let WorldFromName =
    Worlds
    |> Array.map (fun x -> x.WorldName, x)
    |> readOnlyDict

let WorldToDC = 
    seq {
        for dc, worlds in DataCenters do 
            for world in worlds do 
                yield WorldFromName.[world], dc
    } |> readOnlyDict
    