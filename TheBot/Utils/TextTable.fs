module TheBot.Utils.TextTable

open System
open System.Collections.Generic
open KPX.FsCqHttp.Handler

type TextTable(cols : int) =
    let preTableLines  = List<string>()
    let postTableLines = List<string>()

    let col = Array.init cols (fun _ -> List<string>())

    static let halfWidthSpace = ' '
    static let fullWidthSpace = '　'

    static let charLen (c) =
        // 007E是ASCII中最后一个可显示字符
        if c <= '~' then 1
        else 2

    static let strDispLen (str : string) =
        str.ToCharArray()
        |> Array.sumBy charLen

    static member FromHeader(header : Object []) =
        let x = TextTable(header.Length)
        x.AddRow(header)
        x

    member x.AddPreTable(str : string) =  preTableLines.Add(str)

    member x.AddPreTable(tt : TextTable) = 
        for line in tt.ToLines() do 
            preTableLines.Add(line)

    member x.AddPostTable(str : string) = postTableLines.Add(str)

    member x.AddPostTable(tt : TextTable) = 
        for line in tt.ToLines() do 
        postTableLines.Add(line)

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
            col.[i].Add(toStr(o)))

    member x.ToLines() = 
        [|
            yield! preTableLines.ToArray()
            if col.[0].Count <> 0 then
                let maxLens =
                    col
                    |> Array.map (fun l ->
                        l
                        |> Seq.map (strDispLen)
                        |> Seq.max)
                let sb = Text.StringBuilder()
                for i = 0 to col.[0].Count - 1 do
                    sb.Clear() |> ignore
                    for c = 0 to col.Length - 1 do
                        let str = col.[c].[i]
                        let padCharLen = (maxLens.[c] - strDispLen (str))
                        let padFullChr = "".PadLeft(padCharLen / 2 + 1, fullWidthSpace)
                        let padHalfChr = "".PadLeft(padCharLen % 2, halfWidthSpace)
                        //let pad = (maxLens.[c] - strDispLen (str)) / 2 + 1 + str.Length
                        //sb.Append(str.PadRight(pad, fullWidthSpace)) |> ignore
                        sb.Append(str).Append(padFullChr).Append(padHalfChr) |> ignore
                    yield sb.ToString()
            yield! postTableLines.ToArray()
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
        for line in tt.ToLines() do 
            x.WriteLine(line)