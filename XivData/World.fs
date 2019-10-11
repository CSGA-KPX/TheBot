module XivData.World
open System

type World = 
    {
        WorldId   : uint16
        WorldName : string
    }

let Worlds = 
    [|
        //一区，鸟服
        1167us, "红玉海"
        1169us, "延夏"
        1060us, "萌芽池"
        1106us, "静语庄园"
        1081us, "神意之地"
        1045us, "摩杜纳"
        1044us, "幻影群岛"
        1043us, "紫水栈桥"
        1042us, "拉诺西亚"

        //二区，莫古力
        1076us, "白金幻象"
        1172us, "白银乡"
        1171us, "神拳痕"
        1170us, "潮风亭"
        1113us, "旅人栈桥"
    |]
    |> Array.map (fun (id,n) -> {WorldId =id; WorldName = n})

let WorldFromId =
    Worlds
    |> Array.map (fun x -> x.WorldId, x)
    |> readOnlyDict

let WorldFromName =
    Worlds
    |> Array.map (fun x -> x.WorldName, x)
    |> readOnlyDict