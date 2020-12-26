namespace KPX.FsCqHttp.Utils.TextTable

open System


type NumberFormatOptions =
    { /// 保留的有效位数
      SigDigits : int
      /// 舍去小数部分
      TruncateDigits : bool
      /// 是否右对齐
      RightAlign : bool
      /// 数字0的替代文本
      ZeroString : string
      /// Nan的替代文本
      NanString : string }

    /// 4位 含小数 右对齐
    static member Sig4Float =
        { SigDigits = 4
          TruncateDigits = false
          RightAlign = true
          ZeroString = "0"
          NanString = "NaN" }

    /// 4位 含小数 右对齐
    static member Sig4Integer =
        { SigDigits = 4
          TruncateDigits = true
          RightAlign = true
          ZeroString = "0"
          NanString = "NaN" }

    /// 不进位 含小数 右对齐
    static member DefaultFloat =
        { SigDigits = 0
          TruncateDigits = false
          RightAlign = true
          ZeroString = "0"
          NanString = "NaN" }

    /// （不进位 无小数 右对齐)
    static member DefaultInteger =
        { SigDigits = 0
          TruncateDigits = true
          RightAlign = true
          ZeroString = "0"
          NanString = "NaN" }

[<Sealed>]
/// 提供一些排版用的辅助函数
type TableHelpers =
    /// 进位到指定有效数字， 默认4位，如果为0返回原始值
    static member RoundSigDigits(value : float, ?sigDigits : int) =
        let sigDigits = defaultArg sigDigits 4

        if value = 0.0 then
            0.0
        elif sigDigits = 0 then
            value
        else
            let scale =
                10.0 ** ((value |> abs |> log10 |> floor) + 1.0)

            scale * Math.Round(value / scale, sigDigits)

    /// 进位到指定有效数字， 默认4位
    static member RoundSigDigits(value : int, ?sigDigits : int) =
        let sigDigits = defaultArg sigDigits 4
        TableHelpers.RoundSigDigits(float value, sigDigits)

    /// 格式化为人类友好的显示方式。默认使用NumberFormatOptions.DefaultFloat
    /// 含千分位，大于一亿按亿计算
    static member HumanReadable(d : float, ?opts : NumberFormatOptions) =
        let opts =
            defaultArg opts NumberFormatOptions.DefaultFloat

        let right = opts.RightAlign

        if d = 0.0 then
            let zeroString = opts.ZeroString

            if right then TableCell.CreateRightAlign(zeroString) else TableCell.CreateLeftAlign(zeroString)
        elif Double.IsNaN(d) then
            let nanString = opts.NanString

            if right then TableCell.CreateRightAlign(nanString) else TableCell.CreateLeftAlign(nanString)
        else
            let d =
                TableHelpers.RoundSigDigits(d, opts.SigDigits)

            let s =
                10.0 ** ((d |> abs |> log10 |> floor) + 1.0)

            let l = log10 s |> floor |> int
            let (scale, postfix) = if l >= 9 then 8.0, "亿" else 0.0, ""

            let str =
                if (l - (int scale) + 1) >= opts.SigDigits then
                    String.Format("{0:N0}{1}", d / 10.0 ** scale, postfix)
                else
                    String.Format("{0:N2}{1}", d / 10.0 ** scale, postfix)

            if right then TableCell.CreateRightAlign(str) else TableCell.CreateLeftAlign(str)

    /// 格式化为人类友好的显示方式。默认使用NumberFormatOptions.Default
    /// 含千分位，大于一亿按亿计算
    static member HumanReadable(d : int, ?opts : NumberFormatOptions) =
        let opts =
            defaultArg opts NumberFormatOptions.DefaultFloat

        TableHelpers.HumanReadable(float d, opts)

    static member HumanReadableInteger(d : float) =
        TableHelpers.HumanReadable(d, NumberFormatOptions.DefaultInteger)

    static member HumanReadableInteger(d : int) =
        TableHelpers.HumanReadable(d, NumberFormatOptions.DefaultInteger)

    static member HumanReadableFloat(d : float) =
        TableHelpers.HumanReadable(d, NumberFormatOptions.DefaultFloat)

    static member HumanReadableFloat(d : int) =
        TableHelpers.HumanReadable(d, NumberFormatOptions.DefaultFloat)

    static member HumanReadableSig4Float(d : float) =
        TableHelpers.HumanReadable(d, NumberFormatOptions.Sig4Float)

    static member HumanReadableSig4Float(d : int) =
        TableHelpers.HumanReadable(d, NumberFormatOptions.Sig4Float)

    static member HumanReadableSig4Int(d : float) =
        TableHelpers.HumanReadable(d, NumberFormatOptions.Sig4Integer)

    static member HumanReadableSig4Int(d : int) =
        TableHelpers.HumanReadable(d, NumberFormatOptions.Sig4Integer)
