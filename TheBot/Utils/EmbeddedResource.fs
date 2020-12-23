module KPX.TheBot.Utils.EmbeddedResource

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open System.Reflection

let GetResourceManager (str) =
    Resources.ResourceManager("TheBot.Resources." + str, Assembly.GetExecutingAssembly())

let GetResFileStream (filename) =
    let resName = "TheBot.Resources." + filename
    let assembly = Assembly.GetExecutingAssembly()
    assembly.GetManifestResourceStream(resName)

let private emptyChars = [| '\r'; '\n' |]

type StringResource(resxName : string) =
    let mgr = GetResourceManager(resxName)

    member x.GetString(key : string) = mgr.GetString(key)

    /// 返回所有行
    member x.GetLines(key : string) =
        x
            .GetString(key)
            .Split(emptyChars, StringSplitOptions.RemoveEmptyEntries)

    /// 返回所有不以cmtStart开始的行
    member x.GetLinesWithoutCommand(key : string, ?cmtStart : string) =
        let cmtStart = defaultArg cmtStart "//"

        x.GetLines(key)
        |> Array.filter (fun line -> not <| line.StartsWith(cmtStart))

[<AbstractClass>]
type StringTemplate() =
    // \n \r
    // \{eval expr [noexpr]}
    // \{randomItem arrayName}
    static let regex =
        Regex(
            @"(?<newline>\\n|\\r|\\r\\n)|\\\{(?<expr>[^\}]*)\}",
            RegexOptions.Compiled
            ||| RegexOptions.Multiline
            ||| RegexOptions.IgnoreCase
        )

    abstract ProcessFunctions : name:string * args:string [] -> string

    member private x.EvalFunctionCore(m : Match) =
        if m.Groups.["newline"].Success then
            Environment.NewLine
        else
            let expr = m.Groups.["expr"].Value.Split(" ")
            let name = expr.[0]
            let args = expr.[1..]
            x.ProcessFunctions(name, args)

    member x.ParseTemplate(template : string) =
        regex.Replace(template, MatchEvaluator x.EvalFunctionCore)
