[<RequireQualifiedAccess>]
module KPX.FsCqHttp.Config

[<RequireQualifiedAccess>]
module Debug = 
    let mutable Enable = false

[<RequireQualifiedAccess>]
module Logging = 
    /// 是否记录事件上报
    let mutable LogEventPost = false
    /// 是否记录API调用
    let mutable LogApiCall = true
    /// 是否记录指令事件
    let mutable LogCommandCall = true

[<RequireQualifiedAccess>]
module Command = 
    let CommandStart = "#"

[<RequireQualifiedAccess>]
module Output = 
    open System.Text.RegularExpressions
    /// 文本回复下最长输出字符数
    let mutable TextLengthLimit = 3000

    /// 图片输出下使用的字体
    // Mono上GDI+处理和Windows不同，字体渲染存在差异
    // 目前已知Sarasa Fixed CL字体输出比较稳定，其他不明
    let mutable ImageOutputFont = "Sarasa Fixed CL"
    let mutable ImageOutputSize = 12.0f

    /// 调整字符显示宽度。如果IsMatch=true则认为是1栏宽
    ///
    /// 需要设置RegexOptions.Compiled
    let mutable CharDisplayLengthAdj =
        Regex(@"\p{IsBasicLatin}|\p{IsGeneralPunctuation}|±", RegexOptions.Compiled)