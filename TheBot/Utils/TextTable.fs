module TheBot.Utils.TextTable

open System
open System.Collections.Generic
open KPX.FsCqHttp.Handler

/// 延迟TextTable的求值时间点，便于在最终输出前对TextTable的参数进行调整
type private DelayedTableItem =
    | StringItem of string
    | TableItem of TextTable

and TextTable(cols : int) =
    let preTableLines  = List<DelayedTableItem>()
    let postTableLines = List<DelayedTableItem>()

    let col = Array.init cols (fun _ -> List<string>())

    static let halfWidthSpace = ' '

    static let charLenRegex = 
        Text.RegularExpressions.Regex(
            @"\p{IsBasicLatin}|\p{IsGeneralPunctuation}",
            Text.RegularExpressions.RegexOptions.Compiled )

    static let charLen (c) =
        if charLenRegex.IsMatch(c.ToString()) then 1 else 2

    static let strDispLen (str : string) =
        str.ToCharArray()
        |> Array.sumBy charLen

    static member FromHeader(header : Object []) =
        let x = TextTable(header.Length)
        x.AddRow(header)
        x

    /// 获取或设置用于制表的字符，默认为全角空格
    member val PaddingChar = '　' with get, set

    member x.AddPreTable(str : string) = preTableLines.Add(StringItem str)

    member x.AddPreTable(tt : TextTable) = preTableLines.Add(TableItem tt)

    member x.AddPostTable(str : string) = postTableLines.Add(StringItem str)

    member x.AddPostTable(tt : TextTable) = postTableLines.Add(TableItem tt)

    /// 添加一行，用"--"补齐不足行数
    member x.AddRowPadding([<ParamArray>] fields : Object []) = 
        if fields.Length > col.Length then
            raise <| ArgumentException(sprintf "列数不一致 需求:%i, 提供:%i" col.Length fields.Length)
        let padding = Array.create (col.Length - fields.Length) (box "--")
        x.AddRow(Array.append fields padding)

    member x.AddRow([<ParamArray>] fields : Object []) =
        if fields.Length <> col.Length then
            raise <| ArgumentException(sprintf "列数不一致 需求:%i, 提供:%i" col.Length fields.Length)
        fields
        |> Array.iteri (fun i o ->
            let rec toStr(o : obj) =
                match o with
                | :? string as str -> str
                | :? int32 as i -> System.String.Format("{0:N0}", i)
                | :? uint32 as i -> System.String.Format("{0:N0}", i)
                | :? float as f ->
                    let fmt =
                        if (f % 1.0) <> 0.0 then "{0:N2}"
                        else "{0:N0}"
                    System.String.Format(fmt, f)
                | :? TimeSpan as ts ->
                    sprintf "%i天%i时%i分前" ts.Days ts.Hours ts.Minutes
                | :? DateTimeOffset as dto ->
                    toStr(DateTimeOffset.Now - dto)
                | :? DateTime as dt ->
                    toStr(DateTime.Now - dt)
                | _ -> o.ToString()

            // 如果文本显示长度是奇数，补一个空格
            let ret = toStr(o)
            let padLeft = if i = 0 then 0 else 1 // 最左边一列不补
            let padRight = (strDispLen(ret) % 2) + 1
            let str = String(halfWidthSpace, padLeft) + ret + String(halfWidthSpace, padRight)
            col.[i].Add(str))

    member private x.ExpandItems(l : List<DelayedTableItem>) = 
        seq {
            for item in l do 
                match item with
                | StringItem str -> yield str
                | TableItem tt ->
                    tt.PaddingChar <- x.PaddingChar
                    yield! tt.ToLines()
        }
        

    member x.ToLines() = 
        [|
            yield! x.ExpandItems(preTableLines)
            if col.[0].Count <> 0 then // 至少得有一行
                let maxLens =          // 每一列里最长的一格有多长
                    col
                    |> Array.map (fun l ->
                        l
                        |> Seq.map (strDispLen)
                        |> Seq.max )
                let sb = Text.StringBuilder()
                let padLen = strDispLen(x.PaddingChar |> string)
                for i = 0 to col.[0].Count - 1 do
                    sb.Clear() |> ignore
                    for c = 0 to col.Length - 1 do
                        let str = col.[c].[i]
                        let maxLen = maxLens.[c]
                        let strLen = strDispLen(str)
                        let padFull = (maxLen - strLen) / padLen
                        let padding = String(x.PaddingChar, padFull)
                        sb.Append(str).Append(padding) |> ignore

                    yield sb.ToString()
            yield! x.ExpandItems(postTableLines)
        |]

    override x.ToString() =
        String.Join("\r\n", x.ToLines())


type AutoTextTable<'T>(cfg : (string * ('T -> obj)) []) as x = 
    inherit TextTable(cfg.Length)

    do
        x.AddRow(cfg |> Array.map (fst >> box))

    member x.AddObject(obj : 'T) = 
        let objs = 
            cfg
            |> Array.map (fun (_, func) -> func obj)
        x.AddRow(objs)

type TextResponse with
    member x.Write(tt : TextTable) = 
        if x.PreferImageOutput && x.CanSendImage() then tt.PaddingChar <- ' '
        for line in tt.ToLines() do 
            x.WriteLine(line)