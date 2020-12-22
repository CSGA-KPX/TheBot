module KPX.FsCqHttp.Utils.TextTable

open System
open System.Collections.Generic


/// 用于区分显示细节
[<DefaultAugmentation(false)>]
type CellType =
    /// 左对齐单元格，用于文本类型
    | LeftAlignCell of string
    /// 右对齐单元格，用于数字类型
    | RightAlignCell of string

    member x.IsRightAlign =
        match x with
        | RightAlignCell _ -> true
        | _ -> false

    member x.IsLeftAlign =
        match x with
        | RightAlignCell _ -> false
        | _ -> true

    /// 获取单元格的原始内容
    member x.Value =
        match x with
        | RightAlignCell v -> v
        | LeftAlignCell v -> v

    /// 文本显示长度
    member x.DisplayWidth =
        KPX.FsCqHttp.Config.Output.TextTable.StrDispLen(x.ToString())

    static member private IsObjectNumeric(o : obj) =
        match o with
        | :? TimeSpan -> true
        | _ ->
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
            | TypeCode.DateTime -> true
            | _ -> false

    static member private ConvertToString(o : obj) =
        match o with
        | :? string as str -> str
        | :? int32 as i -> System.String.Format("{0:N0}", i)
        | :? uint32 as i -> System.String.Format("{0:N0}", i)
        | :? float as f ->
            let fmt =
                if (f % 1.0) <> 0.0 then "{0:N2}" else "{0:N0}"

            System.String.Format(fmt, f)
        | :? TimeSpan as ts ->
            if ts.TotalDays >= 1.0 then sprintf "%.0f天前" ts.TotalDays
            elif ts.TotalHours >= 1.0 then sprintf "%.0f时前" ts.TotalHours
            elif ts.TotalMinutes >= 1.0 then sprintf "%.0f分前" ts.TotalMinutes
            else "刚刚"
        | :? DateTimeOffset as dto -> CellType.ConvertToString(DateTimeOffset.Now - dto)
        | :? DateTime as dt -> CellType.ConvertToString(DateTime.Now - dt)
        | _ -> o.ToString()

    /// 根据对象类型，创建适合的单元格
    static member CreateFrom(o : obj) =
        if o :? CellType then
            o :?> CellType
        else
            let str = CellType.ConvertToString(o)
            let isNum = CellType.IsObjectNumeric(o)

            if isNum then RightAlignCell str else LeftAlignCell str

    /// 强制为左对齐
    static member CreateLeftAlign(o : obj) =
        match CellType.CreateFrom(o) with
        | RightAlignCell x -> LeftAlignCell x
        | x -> x

    /// 强制为右对齐
    static member CreateRightAlign(o : obj) =
        match CellType.CreateFrom(o) with
        | LeftAlignCell x -> RightAlignCell x
        | x -> x

type internal TextColumn() =
    inherit List<CellType>()

    let mutable defaultLeftAlignment = true

    /// 设置默认为左对齐
    member x.SetLeftAlignment() = defaultLeftAlignment <- true

    /// 设置默认为右对齐
    member x.SetRightAlignment() = defaultLeftAlignment <- false

    /// 添加为默认对齐方式
    member x.AddDefaultAlignment(o : obj) =
        let add =
            if defaultLeftAlignment then CellType.CreateLeftAlign(o) else CellType.CreateRightAlign(o)

        x.Add(add)

    /// 将列内所有单元格重置为左对齐
    member x.ForceLeftAlign() =
        x.SetLeftAlignment()
        for i = 0 to x.Count - 1 do
            x.[i] <- LeftAlignCell x.[i].Value

    /// 将列内所有单元格重置为右对齐
    member x.ForceRightAlign() =
        x.SetRightAlignment()
        for i = 0 to x.Count - 1 do
            x.[i] <- RightAlignCell x.[i].Value

    member x.GetMaxDisplayWidth() =
        x
        |> Seq.map (fun cell -> cell.DisplayWidth)
        |> Seq.max

    /// 对齐到指定大小
    member x.DoAlignment(padChar : char) =
        let max = x.GetMaxDisplayWidth()

        let padCharLen =
            KPX.FsCqHttp.Config.Output.TextTable.CharLen(padChar)
        for i = 0 to x.Count - 1 do
            let cell = x.[i]
            let width = cell.DisplayWidth
            let padLen = (max - width) / padCharLen // 整数部分用padChar补齐
            let rstLen = (max - width) % padCharLen // 非整数部分用空格补齐

            if padLen <> 0 || rstLen <> 0 then
                let padding =
                    String(padChar, padLen) + String(' ', rstLen)
                x.[i] <- if cell.IsLeftAlign then
                             LeftAlignCell(cell.Value + padding)
                         else
                             RightAlignCell(padding + cell.Value)


[<Struct>]
type RowBuilderType = internal B of (List<CellType> -> unit)

type RowBuilder() =
    let (!) =
        function
        | B f -> f

    member _.Yield(value : obj) =
        B(fun b -> b.Add(CellType.CreateFrom(value)))

    member _.Combine(f, g) =
        B
            (fun b ->
                ! f b
                ! g b)

    member _.Delay f = B(fun b -> ! (f ()) b)
    member _.Zero() = B(fun _ -> ())

    member _.For(xs : 'a seq, f : 'a -> RowBuilderType) =
        B
            (fun b ->
                let e = xs.GetEnumerator()

                while e.MoveNext() do
                    ! (f e.Current) b)

    member _.While(p : unit -> bool, f : RowBuilderType) =
        B
            (fun b ->
                while p () do
                    ! f b)

/// 延迟TextTable的求值时间点，便于在最终输出前对TextTable的参数进行调整
type private DelayedTableItem =
    | StringItem of string
    | TableItem of TextTable

and TextTable([<ParamArray>] header : Object []) =
    static let builder = RowBuilder()

    let preTableLines = List<DelayedTableItem>()
    let postTableLines = List<DelayedTableItem>()

    let colCount = header.Length
    let cols = List<TextColumn>(colCount)

    do
        for _ = 0 to colCount - 1 do
            cols.Add(TextColumn())

        header
        |> Array.iteri
            (fun i o ->
                let cell = CellType.CreateFrom(o)
                cols.[i].Add(cell)
                // 根据表头设置默认的对齐类型
                if cell.IsLeftAlign then cols.[i].SetLeftAlignment() else cols.[i].SetRightAlignment())

    member val ColumnPaddingChar = KPX.FsCqHttp.Config.Output.TextTable.FullWidthSpace with get, set

    member x.AddPreTable(str : string) = preTableLines.Add(StringItem str)
    member x.AddPreTable(tt : TextTable) = preTableLines.Add(TableItem tt)

    member x.AddPostTable(str : string) = postTableLines.Add(StringItem str)
    member x.AddPostTable(tt : TextTable) = postTableLines.Add(TableItem tt)

    /// 获取用于行构造器的对象
    member x.RowBuilder = builder

    member x.AddRow(builderType : RowBuilderType) =
        let (B builder) = builderType
        let fields = List<_>()
        builder fields

        if fields.Count <> colCount
        then invalidArg (nameof builder) (sprintf "数量不足：需求%i 提供%i" colCount fields.Count)

        fields |> Seq.iteri (fun i c -> cols.[i].Add(c))

    member x.AddRow([<ParamArray>] objs : Object []) =
        if objs.Length <> colCount
        then invalidArg (nameof objs) (sprintf "数量不足：需求%i 提供%i" colCount objs.Length)

        objs
        |> Array.iteri (fun i o -> cols.[i].Add(CellType.CreateFrom(o)))

    /// 使用指定字符串填充未使用的单元格
    member x.AddRowFill([<ParamArray>] objs : Object []) =
        if objs.Length > colCount
        then invalidArg (nameof objs) (sprintf "输入过多：需求%i 提供%i" colCount objs.Length)

        for i = 0 to objs.Length - 1 do
            cols.[i].Add(CellType.CreateFrom(objs.[i]))

        let def =
            box KPX.FsCqHttp.Config.Output.TextTable.CellPadding
        for i = objs.Length to colCount - 1 do
            cols.[i].AddDefaultAlignment(def)

    member x.ToLines() =
        [| let expand (l : List<DelayedTableItem>) =
            seq {
                for item in l do
                    match item with
                    | StringItem str -> yield str
                    | TableItem tt ->
                        tt.ColumnPaddingChar <- x.ColumnPaddingChar
                        yield! tt.ToLines()
            }

           yield! expand preTableLines

           for col in cols do
               col.DoAlignment(x.ColumnPaddingChar)

           let interColumnPadding = sprintf " %c " x.ColumnPaddingChar
           let sb = Text.StringBuilder()
           for row = 0 to cols.[0].Count - 1 do
               sb.Clear().Append(cols.[0].[row].Value) |> ignore
               for col = 1 to colCount - 1 do
                   sb
                       .Append(interColumnPadding)
                       .Append(cols.[col].[row].Value)
                   |> ignore

               yield sb.ToString()

           yield! expand postTableLines |]


    override x.ToString() = String.Join("\r\n", x.ToLines())

type KPX.FsCqHttp.Utils.TextResponse.TextResponse with
    member x.Write(tt : TextTable) =
        if x.DoSendImage then tt.ColumnPaddingChar <- ' '

        for line in tt.ToLines() do
            x.WriteLine(line)
