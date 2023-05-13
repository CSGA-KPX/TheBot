namespace KPX.TheBot.Host.Data

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Text.RegularExpressions
open System.Resources
open System.Runtime.CompilerServices

open LiteDB


(*
    cacheDir : 存放数据库缓存等数据，可以通过##rebuilddatacahce指令清除
    persistDir : 存放永久数据
*)

[<AutoOpen>]
module LiteDBExtensions =
    type ILiteCollection<'T> with

        member x.SafeFindById(id: BsonValue) =
            let ret = x.FindById(id)

            if isNull (box ret) then
                let msg = $"不能在%s{x.Name}中找到%A{id}"
                raise <| KeyNotFoundException(msg)

            ret

        member x.TryFindById(id: BsonValue) =
            let ret = x.FindById(id)

            if isNull (box ret) then
                None
            else
                Some ret

        member x.TryFindOne(query: Query) =
            let ret = x.FindOne(query)

            if isNull (box ret) then
                None
            else
                Some ret

        member x.TryFindOne(expr: BsonExpression) =
            let ret = x.FindOne(expr)

            if isNull (box ret) then
                None
            else
                Some ret

        member x.SafeFindOne(query: Query) =
            x.TryFindOne(query).Value

        member x.SafeFindOne(expr: BsonExpression) =
            x.TryFindOne(expr).Value

type DataAgent private () =
    static let hostPath =
        let location = Assembly.GetExecutingAssembly().Location
        Path.GetDirectoryName(location)

    static let cacheDir = Path.Combine(hostPath, "./cache/")

    static let persistDir = Path.Combine(hostPath, "../persist/")

    static let dbCache = Dictionary<string, LiteDatabase>()

    static let getDb (path) =
        if not <| dbCache.ContainsKey(path) then
            let dbFile = $"Filename=%s{path};"
            let db = new LiteDatabase(dbFile)
            dbCache.Add(path, db)

        dbCache.[path]

    static do
        Directory.CreateDirectory(cacheDir) |> ignore
        Directory.CreateDirectory(persistDir) |> ignore
        BsonMapper.Global.EmptyStringToNull <- false
        BsonMapper.Global.EnumAsInteger <- true

    static member GetCacheFile(fileName: string) = Path.Combine(cacheDir, fileName)

    static member GetPersistFile(fileName: string) = Path.Combine(persistDir, fileName)

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    static member GetCacheDatabase() =
        let assembly = Assembly.GetCallingAssembly().GetName().Name
        let path = DataAgent.GetCacheFile($"theBotCache-{assembly}")
        getDb (path)

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    static member GetPersistDatabase(fileName: string) =
        let path = DataAgent.GetPersistFile(fileName)
        getDb (path)

module EmbeddedResource =
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let GetResFileStream resName =
        let assembly = Assembly.GetCallingAssembly()
        assembly.GetManifestResourceStream(resName)

type ResxManager(name, asm) =
    inherit ResourceManager(name, asm)

    let newLines = [| '\r'; '\n' |]

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    new(name) = ResxManager(name, Assembly.GetCallingAssembly())

    /// 返回空白字符分隔后的词
    member x.GetWords(key: string) =
        x
            .GetString(key)
            .Split(Array.empty<char>, StringSplitOptions.RemoveEmptyEntries)

    /// 返回所有行
    member x.GetLines(key: string) =
        x
            .GetString(key)
            .Split(newLines, StringSplitOptions.RemoveEmptyEntries)

    /// 返回所有不以cmtStart开始的行
    member x.GetLinesWithoutComment(key: string, ?cmtStart: string) =
        let cmtStart = defaultArg cmtStart "//"

        x.GetLines(key) |> Array.filter (fun line -> not <| line.StartsWith(cmtStart))

    member x.GetWordsWithoutComment(key: string, ?cmdStart: string) =
        [| let cmtStart = defaultArg cmdStart "//"

           for line in x.GetLinesWithoutComment(key, cmtStart) do
               yield! line.Split(Array.empty<char>, StringSplitOptions.RemoveEmptyEntries) |]

[<AbstractClass>]
type StringTemplate() =
    // \n \r
    // \{eval expr [noexpr]}
    // \{randomItem arrayName}
    static let regex =
        Regex(
            @"(?<newline>\\n|\\r|\\r\\n)|\\\{(?<expr>[^\}]*)\}",
            RegexOptions.Compiled ||| RegexOptions.Multiline ||| RegexOptions.IgnoreCase
        )

    abstract ProcessFunctions: name: string * args: string [] -> string

    member private x.EvalFunctionCore(m: Match) =
        if m.Groups.["newline"].Success then
            Environment.NewLine
        else
            let expr = m.Groups.["expr"].Value.Split(" ")
            let name = expr.[0]
            let args = expr.[1..]
            x.ProcessFunctions(name, args)

    member x.ParseTemplate(template: string) =
        regex.Replace(template, MatchEvaluator x.EvalFunctionCore)
