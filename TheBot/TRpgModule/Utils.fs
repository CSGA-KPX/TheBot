module TheBot.Module.TRpgModule.TRpgUtils

open System
open System.Collections.Generic
open System.Text.RegularExpressions

open TheBot.Module.DiceModule.Utils.DiceExpression

let TRpgDb =
    let FsMapper = LiteDB.FSharp.FSharpBsonMapper()
    let dbFile = @"../static/trpg.db"
    new LiteDB.LiteDatabase(dbFile, FsMapper)

[<RequireQualifiedAccess>]
module StringData = 
    let private data = Dictionary<string, string []>()

    let private rm = TheBot.Utils.EmbeddedResource.GetResourceManager("TRpg")
    let private emptyChars = [| '\r'; '\n' |]

    let GetString (name : string) = 
        rm.GetString(name)

    let GetLines (name : string) = 
        if not <| data.ContainsKey(name) then
            let value = rm.GetString(name)
                            .Split(emptyChars, StringSplitOptions.RemoveEmptyEntries)
            data.Add(name, value)
        data.[name]

    let ChrJobs = GetLines "职业"
    let Key_ChrBackground = "人物背景"
    let Key_TI = "即时症状"
    let Key_LI = "总结症状"
    let Key_SkillAlias = "技能别名"
    let Key_DefaultSkillValues = "默认技能数值"

// \n \r
// \{eval expr [noexpr]}
// \{randomItem arrayName}
let private regex = Regex(@"(?<newline>\\n|\\r|\\r\\n)|\\\{(?<expr>[^\}]*)\}",
                        RegexOptions.Compiled ||| 
                            RegexOptions.Multiline ||| 
                            RegexOptions.IgnoreCase)

let ParseTemplate (str : string, de : DiceExpression) = 
    let evalFunc (m : Match) = 
        if m.Groups.["newline"].Success then
            Environment.NewLine
        else
            let expr = m.Groups.["expr"].Value.Split(" ")
            let ret = 
                match expr.[0] with
                | "eval" -> 
                    let noExpr = expr |> Array.tryFind (fun x -> x = "noexpr")
                    if noExpr.IsNone then
                        sprintf "{%s = %O}" (expr.[1]) (de.Eval(expr.[1]).Sum)
                    else
                        sprintf "%O" (de.Eval(expr.[1]).Sum)
                | "randomItem" -> 
                    try
                        let count = expr |> Array.tryItem 2 |> Option.defaultValue "1" |> Int32.Parse
                        let input = StringData.GetLines(expr.[1])
                        let items = de.Dicer.GetRandomItems(input, count)
                        String.Join(" ", items)
                    with
                    | e -> failwithf "找不到请求的数据集 %s : %s" expr.[1] e.Message

                | unk -> failwithf "unknown cmd : %s" unk
            ret

    regex.Replace(str, MatchEvaluator evalFunc)

type Difficulty = 
    | Critical
    | Extreme
    | Hard
    | Regular
    | Fail
    | Fumble