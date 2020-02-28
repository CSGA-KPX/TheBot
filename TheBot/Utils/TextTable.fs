module TheBot.Utils.TextTable

open System
open System.Collections.Generic
open KPX.FsCqHttp.Handler

type TextTable(cols : int) =
    let col = Array.init cols (fun _ -> List<string>())
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

    member x.AddRow([<ParamArray>] fields : Object []) =
        if fields.Length <> col.Length then
            raise <| ArgumentException(sprintf "列数不一致 需求:%i, 提供:%i" col.Length fields.Length)
        fields
        |> Array.iteri (fun i o ->
            let str =
                match o with
                | :? string as str -> str
                | :? int32 as i -> System.String.Format("{0:N0}", i)
                | :? uint32 as i -> System.String.Format("{0:N0}", i)
                | :? float as f ->
                    let fmt =
                        if (f % 1.0) <> 0.0 then "{0:N2}"
                        else "{0:N0}"
                    System.String.Format(fmt, f)
                | _ -> o.ToString()
            col.[i].Add(str))

    member x.ToLines() = 
        [|
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
                        let len = maxLens.[c]
                        let pad = (maxLens.[c] - strDispLen (str)) / 2 + 1 + str.Length
                        sb.Append(str.PadRight(pad, fullWidthSpace)) |> ignore
                    yield sb.ToString()
        |]

    override x.ToString() =
        let sb = Text.StringBuilder("")
        if col.[0].Count <> 0 then
            let maxLens =
                col
                |> Array.map (fun l ->
                    l
                    |> Seq.map (strDispLen)
                    |> Seq.max)

            for i = 0 to col.[0].Count - 1 do
                for c = 0 to col.Length - 1 do
                    let str = col.[c].[i]
                    let len = maxLens.[c]
                    let pad = (maxLens.[c] - strDispLen (str)) / 2 + 1 + str.Length
                    sb.Append(str.PadRight(pad, fullWidthSpace)) |> ignore
                sb.AppendLine("") |> ignore
        sb.ToString()

type TextResponse with
    member x.Write(tt : TextTable) = 
        for line in tt.ToLines() do 
            x.WriteLine(line)