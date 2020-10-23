module TheBot.Module.DiceModule.TRpgUtils

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Text.RegularExpressions

open TheBot.Utils.Dicer

open TheBot.Module.DiceModule.Utils.DiceExpression

// \n \r
// \{eval expr}
// \{randomItem arrayName}

let private regex = Regex(@"(?<newline>\\n|\\r|\\r\\n)|\\\{(?<expr>[^\}]*)\}",
                        RegexOptions.Compiled ||| 
                            RegexOptions.Multiline ||| 
                            RegexOptions.IgnoreCase)

let StringData = Dictionary<string, string []>()

let DATA_JOBS = "职业"

let ParseTemplate (str : string, de : DiceExpression) = 
    let evalFunc (m : Match) = 
        if m.Groups.["newline"].Success then
            Environment.NewLine
        else
            let expr = m.Groups.["expr"].Value.Split(" ", 2)
            let ret = 
                match expr.[0] with
                | "eval" -> 
                    sprintf "{%s = %O}" (expr.[1]) (de.Eval(expr.[1]).Sum)
                | "randomItem" -> 
                    let aName = expr.[1]
                    if not <| StringData.ContainsKey(aName) then failwithf "找不到请求的数据集 : %s" aName
                    de.Dicer.GetRandomItem(StringData.[aName])
                | unk -> failwithf "unknown cmd : %s" unk
            ret

    regex.Replace(str, MatchEvaluator evalFunc)

do
    let rm = TheBot.Utils.EmbeddedResource.GetResourceManager("TRpg")
    let emptyChars = [| '\r'; '\n' |]
    let readAndSave (name : string) = 
        let value = rm.GetString(name)
                        .Split(emptyChars, StringSplitOptions.RemoveEmptyEntries)
        StringData.Add(name, value)
    readAndSave("即时症状")
    readAndSave("总结症状")
    readAndSave("恐惧症状")
    readAndSave("狂躁症状")
    readAndSave("职业")
