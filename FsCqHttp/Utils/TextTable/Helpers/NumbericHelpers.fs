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

    member x.Format(value : int) = x.Format(float value)

    member x.Format(value : float) =
        let getCell str =
            if x.RightAlign then RightAlignCell str else LeftAlignCell str

        match value with
        | 0.0 -> getCell x.ZeroString
        | _ when Double.IsNaN(value) -> getCell x.NanString
        | _ when Double.IsNegativeInfinity(value) -> getCell "+inf%"
        | _ when Double.IsPositiveInfinity(value) -> getCell "-inf%"
        | _ when x.SigDigits <> 0 ->
            let rounded =
                NumberFormatOptions.RoundSigDigits(value, x.SigDigits)

            let pow10 =
                ((rounded |> abs |> log10 |> floor) + 1.0)
                |> floor
                |> int

            let (scale, postfix) = if pow10 >= 9 then 8.0, "亿" else 0.0, ""

            let str =
                let hasEnoughDigits = (pow10 - (int scale) + 1) >= x.SigDigits

                if x.TruncateDigits || hasEnoughDigits then
                    String.Format("{0:N0}{1}", rounded / 10.0 ** scale, postfix)
                else
                    String.Format("{0:N2}{1}", rounded / 10.0 ** scale, postfix)

            getCell str
        | _ ->
            let str = String.Format("{0:N2}", value)
            if str.EndsWith(".00") then
                getCell <| String.Format("{0:N0}", value)
            else
                getCell str

    static member private RoundSigDigits(value : float, sigDigits : int) =
        if value = 0.0 then
            0.0
        elif sigDigits = 0 then
            value
        else
            let scale =
                10.0 ** ((value |> abs |> log10 |> floor) + 1.0)

            scale * Math.Round(value / scale, sigDigits)

    static member RoundSigDigits(value : int, sigDigits : int) =
        NumberFormatOptions.RoundSigDigits(float value, sigDigits)
        |> int

[<Sealed>]
[<AutoOpen>]
type NumbericHelpers() =
    /// 保留4位有效数字 含小数 右对齐
    static let Sig4Float =
        { SigDigits = 4
          TruncateDigits = false
          RightAlign = true
          ZeroString = "0"
          NanString = "NaN" }

    /// 保留4位有效数字 含小数 右对齐
    static let Sig4Integer =
        { SigDigits = 4
          TruncateDigits = true
          RightAlign = true
          ZeroString = "0"
          NanString = "NaN" }

    /// 显示全部数字 含小数 右对齐
    static let DefaultFloat =
        { SigDigits = 0
          TruncateDigits = false
          RightAlign = true
          ZeroString = "0"
          NanString = "NaN" }

    /// 显示全部数字 无小数 右对齐
    static let DefaultInteger =
        { SigDigits = 0
          TruncateDigits = true
          RightAlign = true
          ZeroString = "0"
          NanString = "NaN" }

    static member HumanReadableInteger(d : float) = DefaultInteger.Format(d)

    static member HumanReadableInteger(d : int) = DefaultInteger.Format(d)

    static member HumanReadableFloat(d : float) = DefaultFloat.Format(d)

    static member HumanReadableFloat(d : int) = DefaultFloat.Format(d)

    static member HumanReadableSig4Float(d : float) = Sig4Float.Format(d)

    static member HumanReadableSig4Float(d : int) = Sig4Float.Format(d)

    static member HumanReadableSig4Int(d : float) = Sig4Integer.Format(d)

    static member HumanReadableSig4Int(d : int) = Sig4Integer.Format(d)
