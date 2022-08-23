namespace KPX.FsCqHttp.Utils.TextResponse

open System
open System.Collections.Generic


module private CellBuildImpl =
    let inline numberImpl (value: ^Number) =
        let str = String.Format("{0:N2}", value)

        if str.EndsWith(".00") then
            TableCell(str.Remove(str.Length - 3), Align = TextAlignment.Right)
        else
            TableCell(str, Align = TextAlignment.Right)

    let inline humanReadbleImpl (value: ^Number) =
        let sigDigits = 4

        let mutable value = TableCellHelper.RoundSigDigits(Convert.ToDouble(value), sigDigits)

        let str =
            match value with
            | 0.0 -> "0"
            | _ when Double.IsNaN(value) -> "NaN"
            | _ when Double.IsNegativeInfinity(value) -> "+inf%"
            | _ when Double.IsPositiveInfinity(value) -> "-inf%"
            | _ ->
                let pow10 = ((value |> abs |> log10 |> floor) + 1.0) |> floor |> int

                let scale, postfix = if pow10 >= 9 then 8.0, "亿" else 0.0, ""

                let hasEnoughDigits = (pow10 - (int scale) + 1) >= sigDigits

                if hasEnoughDigits then
                    String.Format("{0:N0}{1}", value / 10.0 ** scale, postfix)
                else
                    String.Format("{0:N2}{1}", value / 10.0 ** scale, postfix)

        TableCell(str, Align = TextAlignment.Right)

// Widening happens automatically for int32 to int64, int32 to nativeint, and int32 to double
// 需要int32, int64, float

[<AutoOpen; AbstractClass; Sealed>]
type CellUtils private () =

    /// 使用ToString()的字符串表示
    static member Literal(item: int32) = TableCell(item.ToString())

    /// 使用ToString()的字符串表示
    static member Literal(item: uint32) = TableCell(item.ToString())

    /// 使用ToString()的字符串表示
    static member Literal(item: int64) = TableCell(item.ToString())

    /// 使用ToString()的字符串表示
    static member Literal(item: uint64) = TableCell(item.ToString())

    /// 使用ToString()的字符串表示
    static member Literal(item: float) = TableCell(item.ToString())

    static member Literal(str: string) = TableCell(str)

    /// 使用ToString()的字符串表示
    static member RLiteral(item: int32) =
        TableCell(item.ToString(), Align = TextAlignment.Right)

    /// 使用ToString()的字符串表示
    static member RLiteral(item: uint32) =
        TableCell(item.ToString(), Align = TextAlignment.Right)

    /// 使用ToString()的字符串表示
    static member RLiteral(item: int64) =
        TableCell(item.ToString(), Align = TextAlignment.Right)

    /// 使用ToString()的字符串表示
    static member RLiteral(item: uint64) =
        TableCell(item.ToString(), Align = TextAlignment.Right)

    /// 使用ToString()的字符串表示
    static member RLiteral(item: float) =
        TableCell(item.ToString(), Align = TextAlignment.Right)

    static member RLiteral(str: string) =
        TableCell(str, Align = TextAlignment.Right)

    /// 右对齐，千分位
    static member Integer(value: int32) =
        TableCell(String.Format("{0:N0}", value), Align = TextAlignment.Right)

    /// 右对齐，千分位
    static member Integer(value: int64) =
        TableCell(String.Format("{0:N0}", value), Align = TextAlignment.Right)

    /// 右对齐，千分位
    static member Integer(value: float) =
        TableCell(String.Format("{0:N0}", value), Align = TextAlignment.Right)

    /// 右对齐，千分位，2位小数
    static member Float(value: int32) =
        TableCell(String.Format("{0:N2}", value), Align = TextAlignment.Right)

    /// 右对齐，千分位，2位小数
    static member Float(value: int64) =
        TableCell(String.Format("{0:N2}", value), Align = TextAlignment.Right)

    /// 右对齐，千分位，2位小数
    static member Float(value: float) =
        TableCell(String.Format("{0:N2}", value), Align = TextAlignment.Right)

    /// 右对齐，千分位，不为零则保留2位小数
    static member Number(value: float) = CellBuildImpl.numberImpl value

    /// 右对齐，千分位，不为零则保留2位小数
    static member Number(value: int32) = CellBuildImpl.numberImpl value

    /// 右对齐，千分位，不为零则保留2位小数
    static member Number(value: int64) = CellBuildImpl.numberImpl value

    /// 右对齐，千分位，保留4位有效数字，不为零则保留2位小数
    static member Sig4(value: float) =
        TableCellHelper.RoundSigDigits(value, 4) |> Number

    /// 右对齐，千分位，保留4位有效数字，不为零则保留2位小数
    static member Sig4(value: int32) =
        TableCellHelper.RoundSigDigits(value, 4) |> Number

    /// 右对齐，千分位，保留4位有效数字，不为零则保留2位小数
    static member Sig4(value: int64) =
        TableCellHelper.RoundSigDigits(value, 4) |> Number

    /// 右对齐，千分位，保留4位有效数字，超过一亿按亿计，不为零则保留2位小数
    static member HumanSig4(value: float) = CellBuildImpl.humanReadbleImpl (value)

    /// 右对齐，千分位，保留4位有效数字，超过一亿按亿，不为零则保留2位小数
    static member HumanSig4(value: int32) = CellBuildImpl.humanReadbleImpl (value)

    /// 右对齐，千分位，保留4位有效数字，超过一亿按亿，不为零则保留2位小数
    static member HumanSig4(value: int64) = CellBuildImpl.humanReadbleImpl (value)

    /// 2位小数百分比
    static member Percent(value: float) =
        TableCell(String.Format("{0:P2}", value), Align = TextAlignment.Right)

    /// 转换为时间跨度，无效值记为"--"
    static member TimeSpan(value: DateTimeOffset) =
        let str =
            if value = DateTimeOffset.MaxValue
               || value = DateTimeOffset.MinValue
               || value = DateTimeOffset.UnixEpoch then
                "--"
            else
                TableCellHelper.FormatTimeSpan(DateTimeOffset.Now - value)

        TableCell(str, Align = TextAlignment.Right)

    /// 转换为时间跨度，无效值记为"--"
    static member TimeSpan(value: TimeSpan) =
        TableCell(TableCellHelper.FormatTimeSpan(value), Align = TextAlignment.Right)

    /// 转换为日期格式yyyy/MM/dd HH:mm
    static member DateTime(value: DateTimeOffset) =
        TableCell(value.ToLocalTime().ToString("yyyy/MM/dd HH:mm"), Align = TextAlignment.Right)

    /// 隐蔽右对齐单元格。在图片中透明，在文本中为空格
    static member RightPad =
        TableCell(String.Empty, RenderMode = RenderMode.IgnoreIfImage, Align = TextAlignment.Right)

    /// 隐蔽左对齐单元格。在图片中透明，在文本中为空格
    static member LeftPad = TableCell(String.Empty, RenderMode = RenderMode.IgnoreIfImage, Align = TextAlignment.Left)

    /// 拆分多行文本，默认格式
    static member SplitTextRows(str: string) =
        [ for line in str.Split([| "\r\n"; "\r"; "\n" |], StringSplitOptions.None) do
              TableItem.Line(TableCell(line)) ]

    /// 将seq<TableCell>视为多行单列
    static member AsRows(items: seq<TableCell>) = items |> Seq.map TableItem.Line

    /// 将seq<TableCell>视为单行多列
    static member AsCols(items: seq<TableCell>) = items |> Seq.toArray |> TableItem.Row

    /// 将seq<TableCell>视为多行单列
    static member AsRows(items: TableCell list) = items |> List.map TableItem.Line

    /// 将seq<TableCell>视为单行多列
    static member AsCols(items: TableCell list) = items |> List.toArray |> TableItem.Row
