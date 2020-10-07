module KPX.FsCqHttp.Utils.TextTable

open System
open System.Collections.Generic

open KPX.FsCqHttp.Utils.TextResponse

[<Literal>]
let private halfWidthSpace = ' '

let inline private charLen (c) =
    if KPX.FsCqHttp.Config.Output.CharDisplayLengthAdj.IsMatch(c.ToString()) then 1 else 2

let inline private strDispLen (str : string) =
    str.ToCharArray()
    |> Array.sumBy charLen

/// 用于区分显示细节
[<DefaultAugmentation(false)>]
type CellType = 
    | LeftAlignCell of string
    | RightAlignCell of string

    member x.IsNumeric = match x with | RightAlignCell _ -> true | _ -> false

    /// 文本显示长度
    member x.DisplayLength = strDispLen(x.ToString())

    member x.ToString(i : int) = 
        let ret = match x with | LeftAlignCell str -> str | RightAlignCell str -> str

        let left, right = 
            if x.IsNumeric then
                // 数字：左边补齐，右边一个
                ((strDispLen(ret) % 2) + 1), 1
            else
                // 文本：左边补齐，右边补齐
                (if i = 0 then 0 else 1), ((strDispLen(ret) % 2) + 1)

        String(halfWidthSpace, left) + ret + String(halfWidthSpace, right)

    override x.ToString() = 
        x.ToString(0)

    static member private IsNumericType(o : obj) = 
        match Type.GetTypeCode(o.GetType()) with
        | TypeCode.Byte
        | TypeCode.SByte
        | TypeCode.UInt16
        | TypeCode.UInt32
        | TypeCode.UInt64
        | TypeCode.Int16
        | TypeCode.Int32
        | TypeCode.Int64
        | TypeCode.Decimal
        | TypeCode.Double
        | TypeCode.Single
            -> true
        | _ -> false

    static member Create(o : obj) =
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

        if o :? CellType then
            o :?> CellType
        else
            let str = toStr(o)
            let isNum = CellType.IsNumericType(o)

            if isNum then RightAlignCell str else LeftAlignCell str

/// 延迟TextTable的求值时间点，便于在最终输出前对TextTable的参数进行调整
type private DelayedTableItem =
    | StringItem of string
    | TableItem of TextTable

and TextTable(cols : int) =
    let preTableLines  = List<DelayedTableItem>()
    let postTableLines = List<DelayedTableItem>()

    let cellPadding = box KPX.FsCqHttp.Config.Output.TextTable.CellPadding

    let col = Array.init cols (fun _ -> List<CellType>())

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
        let padding = Array.create (col.Length - fields.Length) (cellPadding)
        x.AddRow(Array.append fields padding)

    member x.AddRow([<ParamArray>] fields : Object []) =
        if fields.Length <> col.Length then
            raise <| ArgumentException(sprintf "列数不一致 需求:%i, 提供:%i" col.Length fields.Length)
        fields
        |> Array.iteri (fun i o -> col.[i].Add(CellType.Create(o)))

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
                        |> Seq.map (fun item -> item.DisplayLength)
                        |> Seq.max )
                let sb = Text.StringBuilder()
                let padLen = strDispLen(x.PaddingChar |> string)
                for i = 0 to col.[0].Count - 1 do
                    sb.Clear() |> ignore
                    for c = 0 to col.Length - 1 do
                        let item = col.[c].[i]
                        let str  = item.ToString(c)
                        let padLength = (maxLens.[c] - item.DisplayLength) / padLen
                        let padding = String(x.PaddingChar, padLength)
                        if item.IsNumeric then
                            sb.Append(padding).Append(str) |> ignore
                        else
                            sb.Append(str).Append(padding) |> ignore

                    yield sb.ToString()
            yield! x.ExpandItems(postTableLines)
        |]

    override x.ToString() =
        String.Join("\r\n", x.ToLines())


type AutoTextTable<'T>(cfg : (CellType * ('T -> obj)) []) as x = 
    inherit TextTable(cfg.Length)
    do
        x.AddRow(cfg |> Array.map (fst >> box))

    new(cfg : (string * ('T -> obj)) []) = 
        let cfg = 
            cfg
            |> Array.map (fun (a, b) -> LeftAlignCell a, b)
        AutoTextTable<'T>(cfg)

    member x.AddObject(obj : 'T) = 
        let objs = 
            cfg
            |> Array.map (fun (_, func) -> func obj)
        x.AddRow(objs)

type TextResponse with
    member x.Write(tt : TextTable) = 
        if x.DoSendImage then tt.PaddingChar <- ' '
        for line in tt.ToLines() do 
            x.WriteLine(line)