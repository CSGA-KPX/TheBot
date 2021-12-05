module KPX.DicePlugin.TRpgModule.Strings

open System
open System.Collections.Generic

open KPX.TheBot.Host.Data

open KPX.DicePlugin.DiceModule.Utils.DiceExpression


[<RequireQualifiedAccess>]
module StringData =
    let private data = Dictionary<string, string []>()

    let mgr = ResxManager("DicePlugin.Resources.TRpg")

    let GetString (name: string) = mgr.GetString(name)

    let GetLines (name: string) =
        if not <| data.ContainsKey(name) then
            let value = mgr.GetLines(name)
            data.Add(name, value)

        data.[name]

    let ChrJobs = GetLines "职业"
    let Key_ChrBackground = "人物背景"
    let Key_TI = "即时症状"
    let Key_LI = "总结症状"
    let Key_SkillAlias = "技能别名"
    let Key_DefaultSkillValues = "默认技能数值"

    let Key_ChsName = "中文名"
    let Key_EngName = "英文名"
    let Key_EngChsName = "英中名"
    let Key_JpnName = "日语名"

type TrpgStringTemplate(de: DiceExpression) =
    inherit StringTemplate()

    member x.ParseByKey(key: string) =
        x.ParseTemplate(StringData.GetString(key))

    override x.ProcessFunctions(name, args) =
        match name with
        | "eval" -> // \{eval 表达式 noexpr}
            let ShowExpr = args |> Array.exists (fun x -> x = "noexpr") |> not

            let expression = args |> Array.tryHead

            if expression.IsNone then
                invalidArg "args" "找不到表达式"

            if ShowExpr then
                $"{{%s{expression.Value} = {de.Eval(expression.Value).Sum}}}"
            else
                $"{de.Eval(expression.Value).Sum}"
        | "randomItem" -> // \{randomItem 数组名称 个数}
            try
                let name = args |> Array.tryHead

                if name.IsNone then
                    invalidArg "args" "没有指定数据集名称"

                let count = args |> Array.tryItem 1 |> Option.defaultValue "1" |> Int32.Parse

                let input = StringData.GetLines(name.Value)
                let items = de.Dicer.GetArrayItem(input, count)
                String.Join(" ", items)
            with
            | e -> failwithf $"找不到请求的数据集 %s{name} : %s{e.Message}"
        | "randomItemOpt" -> // \{randomItemOpt 数组名称 阈值|50 } 1D100
            try
                let name = args |> Array.tryHead

                if name.IsNone then
                    invalidArg "args" "没有指定数据集名称"

                let threshold = args |> Array.tryItem 1 |> Option.defaultValue "50" |> Int32.Parse

                let d100 = de.Dicer.GetPositive(100)

                if d100 >= threshold then
                    de.Dicer.GetArrayItem(StringData.GetLines(name.Value))
                else
                    ""
            with
            | e -> failwithf $"找不到请求的数据集 %s{name} : %s{e.Message}"
        | other -> invalidArg (nameof name) ("未知指令名称" + other)
