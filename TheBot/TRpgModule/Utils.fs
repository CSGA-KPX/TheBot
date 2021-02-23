module KPX.TheBot.Module.TRpgModule.TRpgUtils

open System
open System.Collections.Generic

open KPX.TheBot.Data.Common.Database

open KPX.TheBot.Utils.EmbeddedResource

open KPX.TheBot.Module.DiceModule.Utils.DiceExpression


//let TRpgDb = getLiteDB ("trpg.db")

[<RequireQualifiedAccess>]
module StringData =
    let private data = Dictionary<string, string []>()

    let mgr = StringResource("TRpg")

    let GetString (name : string) = mgr.GetString(name)

    let GetLines (name : string) =
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


type TrpgStringTemplate(de : DiceExpression) =
    inherit StringTemplate()

    member x.ParseByKey(key : string) =
        x.ParseTemplate(StringData.GetString(key))

    override x.ProcessFunctions(name, args) =
        match name with
        | "eval" ->
            let ShowExpr =
                args
                |> Array.exists (fun x -> x = "noexpr")
                |> not

            let expression = args |> Array.tryHead
            if expression.IsNone then invalidArg "args" "找不到表达式"

            if ShowExpr
            then sprintf "{%s = %O}" (expression.Value) (de.Eval(expression.Value).Sum)
            else sprintf "%O" (de.Eval(expression.Value).Sum)
        | "randomItem" ->
            try
                let name = args |> Array.tryHead
                if name.IsNone then invalidArg "args" "没有指定数据集名称"

                let count =
                    args
                    |> Array.tryItem 1
                    |> Option.defaultValue "1"
                    |> Int32.Parse

                let input = StringData.GetLines(name.Value)
                let items = de.Dicer.GetRandomItems(input, count)
                String.Join(" ", items)
            with e -> failwithf "找不到请求的数据集 %s : %s" name e.Message
        | other -> invalidArg (nameof name) ("未知指令名称" + other)

type Difficulty =
    | Critical
    | Extreme
    | Hard
    | Regular
    | Fail
    | Fumble
