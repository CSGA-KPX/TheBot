namespace rec KPX.TheBot.Module.TRpgModule.Coc7


module Coc7Utils =

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

[<Struct>]
type RollResult =
    | Critical
    | Extreme
    | Hard
    | Regular
    | Fail
    | Fumble

    override x.ToString() =
        match x with
        | Critical -> "大成功"
        | Extreme -> "成功：极难"
        | Hard -> "成功：艰难"
        | Regular -> "成功：一般"
        | Fail -> "失败"
        | Fumble -> "大失败"

[<Struct>]
/// 创建指定房规的规则
///
/// 大成功：0+offset 大失败：101-offset
type RollResultRule(offset : int) =

    member x.Describe(i : int, threshold : int) =
        match i with
        | _ when i <= 0 + offset -> Critical
        | _ when i <= threshold / 5 -> Extreme
        | _ when i <= threshold / 2 -> Hard
        | _ when i <= threshold -> Regular
        | _ when i >= 101 - offset -> Fumble
        | _ when i > threshold -> Fail
        | _ -> invalidArg "dice" $"骰值%i{i}不再允许范围内"