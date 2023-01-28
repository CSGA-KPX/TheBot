namespace KPX.FsCqHttp.Utils.TextResponse

open System
open System.Numerics


[<AutoOpen; AbstractClass; Sealed>]
type CellUtils private () =

    static member inline Literal<'Number when INumber<'Number>>(item: 'Number) = TableCell(item.ToString())

    static member inline Literal(str: string) = TableCell(str)

    static member inline RLiteral<'Number when INumber<'Number>>(item: 'Number) =
        TableCell(item.ToString(), Align = TextAlignment.Right)

    static member inline RLiteral(str: string) =
        TableCell(str, Align = TextAlignment.Right)

    /// 右对齐，千分位
    static member inline Integer<'Number when INumber<'Number>>(value: 'Number) =
        TableCell(String.Format("{0:N0}", value), Align = TextAlignment.Right)

    /// 右对齐，千分位，2位小数
    static member inline Float<'Number when INumber<'Number>>(value: 'Number) =
        TableCell(String.Format("{0:N2}", value), Align = TextAlignment.Right)

    /// 右对齐，千分位，不为零则保留2位小数
    static member inline Number<'Number when INumber<'Number>>(value: 'Number) =
        let str = String.Format("{0:N2}", value)

        if str.EndsWith(".00") then
            TableCell(str.Remove(str.Length - 3), Align = TextAlignment.Right)
        else
            TableCell(str, Align = TextAlignment.Right)

    /// 右对齐，千分位，保留4位有效数字，不为零则保留2位小数
    static member inline Sig4<'Number when INumber<'Number> and 'Number: equality>(value: 'Number) =
        TableCellHelper.RoundSigDigits(value, 4) |> Number

    /// 右对齐，千分位，保留4位有效数字，超过一亿按亿计，不为零则保留2位小数
    static member inline HumanSig4<'Number when INumber<'Number>>(value: 'Number) =
        TableCellHelper.HumanReadble(value, false)

    /// 右对齐，千分位，保留4位有效数字，超过一亿按亿计，忽略原始数值的小数点
    static member inline HumanSig4I<'Number when INumber<'Number>>(value: 'Number) =
        let value = Convert.ToDouble(value)
        TableCellHelper.HumanReadble(value, true)

    /// 2位小数百分比
    static member inline Percent(value: float) =
        TableCell(String.Format("{0:P2}", value), Align = TextAlignment.Right)

    /// 转换为时间跨度，无效值记为"--"
    static member inline TimeSpan(value: DateTimeOffset) =
        let str =
            if
                value = DateTimeOffset.MaxValue
                || value = DateTimeOffset.MinValue
                || value = DateTimeOffset.UnixEpoch
            then
                "--"
            else
                TableCellHelper.FormatTimeSpan(DateTimeOffset.Now - value)

        TableCell(str, Align = TextAlignment.Right)

    /// 转换为时间跨度，无效值记为"--"
    static member inline TimeSpan(value: TimeSpan) =
        TableCell(TableCellHelper.FormatTimeSpan(value), Align = TextAlignment.Right)

    /// 转换为日期格式yyyy/MM/dd HH:mm
    static member inline DateTime(value: DateTimeOffset) =
        TableCell(value.ToLocalTime().ToString("yyyy/MM/dd HH:mm"), Align = TextAlignment.Right)

    /// 隐蔽右对齐单元格。在图片中透明，在文本中为空格
    static member inline RightPad =
        TableCell(String.Empty, RenderMode = RenderMode.IgnoreIfImage, Align = TextAlignment.Right)

    /// 隐蔽左对齐单元格。在图片中透明，在文本中为空格
    static member inline LeftPad = TableCell(String.Empty, RenderMode = RenderMode.IgnoreIfImage, Align = TextAlignment.Left)

    /// 拆分多行文本，默认格式
    static member inline SplitTextRows(str: string) =
        [ for line in str.Split([| "\r\n"; "\r"; "\n" |], StringSplitOptions.None) do
              TableItem.Line(TableCell(line)) ]

    /// 将seq<TableCell>视为多行单列
    static member inline AsRows(items: seq<TableCell>) = items |> Seq.map TableItem.Line

    /// 将seq<TableCell>视为单行多列
    static member inline AsCols(items: seq<TableCell>) = items |> Seq.toArray |> TableItem.Row

    /// 将seq<TableCell>视为多行单列
    static member inline AsRows(items: TableCell list) = items |> List.map TableItem.Line

    /// 将seq<TableCell>视为单行多列
    static member inline AsCols(items: TableCell list) = items |> List.toArray |> TableItem.Row
