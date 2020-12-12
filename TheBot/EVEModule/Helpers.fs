module TheBot.Module.EveModule.Utils.Helpers

open System
open System.Net.Http

[<Literal>]
let EveSellTax = 6

[<Literal>]
let EveBuyTax = 4

/// EVE小数进位
///
/// d 输入数字
///
/// n 保留有效位数
let RoundFloat (d : float) (n : int) = 
    if d = 0.0 then 0.0
    else
        let scale = 10.0 ** ((d |> abs |> log10 |> floor) + 1.0)
        scale * Math.Round(d / scale, n)

/// 保留4位有效数字，大于一亿按亿显示，0.0显示"--"
let HumanReadableFloat (d : float) = 
    let sigDigits = 4 //有效位数
    let d = RoundFloat d sigDigits
    if d = 0.0 then "--"
    else
        let s = 10.0 ** ((d |> abs |> log10 |> floor) + 1.0)
        let l = log10 s |> floor |> int
        let (scale, postfix) = 
            if l >= 9 then
                8.0, "亿"
            else
                0.0, ""
        
        if (l - (int scale) + 1) >= sigDigits then
            String.Format("{0:N0}{1}", d / 10.0 ** scale, postfix)
        else
            String.Format("{0:N2}{1}", d / 10.0 ** scale, postfix)
