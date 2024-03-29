﻿module rec KPX.DicePlugin.TRpgModule.Coc7

open System
open System.Collections.Generic
open KPX.DicePlugin.TRpgModule.Strings


let Coc7AttrExpr =
    [| "力量", "3D6*5"
       "体质", "3D6*5"
       "体型", "(2D6+6)*5"
       "敏捷", "3D6*5"
       "外貌", "3D6*5"
       "智力", "(2D6+6)*5"
       "意志", "3D6*5"
       "教育", "(2D6+6)*5"
       "幸运", "3D6*5" |]

let Coc7AttrDisplayOrder =
    [| // 主属性和幸运
       yield "力量"
       yield "体质"
       yield "体型"
       yield "敏捷"
       yield "外貌"
       yield "智力"
       yield "意志"
       yield "教育"
       yield "幸运"
       // 衍生属性
       yield "体力"
       yield "理智"
       yield "魔法"
       yield "移动力"
       yield "护甲"
       yield "闪避"
       yield "体格"
       yield "伤害奖励" |]

/// Coc7中技能别名
let SkillNameAlias =
    let dict = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

    for line in StringData.GetLines(StringData.Key_SkillAlias) do
        let t = line.Split("|")
        dict.Add(t.[0], t.[1])

    dict :> IReadOnlyDictionary<_, _>

/// Coc7中技能及默认值
let DefaultSkillValues =
    let dict = Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)

    for line in StringData.GetLines(StringData.Key_DefaultSkillValues) do
        let t = line.Split("|")
        dict.Add(t.[0], t.[1] |> int)

    dict :> IReadOnlyDictionary<_, _>

/// 将输入转换为Coc7技能名，处理别名等
let MapCoc7SkillName (name: string) =
    if SkillNameAlias.ContainsKey(name) then
        SkillNameAlias.[name]
    else
        name

[<Struct>]
type RollResult =
    | Critical
    | Extreme
    | Hard
    | Regular
    | Fail
    | Fumble

    member x.IsSuccess =
        match x with
        | Critical
        | Extreme
        | Hard
        | Regular -> true
        | _ -> false

    member x.IsFailed = not x.IsSuccess

    override x.ToString() =
        match x with
        | Critical -> "大成功"
        | Extreme -> "成功：极难"
        | Hard -> "成功：艰难"
        | Regular -> "成功：一般"
        | Fail -> "失败"
        | Fumble -> "大失败"

    /// 大成功：0+offset 大失败：101-offset
    static member Describe(i: int, threshold: int, ?offset: int) =
        let offset = defaultArg offset 1

        match i with
        | _ when i <= 0 + offset -> Critical
        | _ when i <= threshold / 5 -> Extreme
        | _ when i <= threshold / 2 -> Hard
        | _ when i <= threshold -> Regular
        | _ when i >= 101 - offset -> Fumble
        | _ when i > threshold -> Fail
        | _ -> invalidArg "dice" $"骰值%i{i}不再允许范围内"

[<Struct>]
/// 创建指定房规的规则
///
/// 大成功：0+offset 大失败：101-offset
type RollResultRule(offset: int) =

    member x.Describe(i: int, threshold: int) =
        match i with
        | _ when i <= 0 + offset -> Critical
        | _ when i <= threshold / 5 -> Extreme
        | _ when i <= threshold / 2 -> Hard
        | _ when i <= threshold -> Regular
        | _ when i >= 101 - offset -> Fumble
        | _ when i > threshold -> Fail
        | _ -> invalidArg "dice" $"骰值%i{i}不再允许范围内"

    /// 规则书评价
    member x.Describe(i: int) = x.Describe(i, 1)
