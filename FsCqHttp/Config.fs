[<RequireQualifiedAccess>]
module KPX.FsCqHttp.Config

[<RequireQualifiedAccess>]
module Logging = 
    let mutable LogEventPost = false
    let mutable LogApiCall = true
    let mutable LogCommandCall = true

[<RequireQualifiedAccess>]
module Command = 
    let CommandStart = "#"

[<RequireQualifiedAccess>]
module Output = 
    /// 文本回复下最长输出字符数
    let mutable TextLengthLimit = 3000

    /// 图片输出下使用的字体
    // Mono上GDI+处理和Windows不同，字体渲染存在差异
    // 目前已知Sarasa Fixed CL字体输出比较稳定，其他不明
    let mutable ImageOutputFont = "Sarasa Fixed CL"
    let mutable ImageOutputSize = 12.0f