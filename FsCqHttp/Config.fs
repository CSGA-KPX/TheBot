[<RequireQualifiedAccess>]
module KPX.FsCqHttp.Config

/// 用于指示配置所在命名空间
type ConfigPlaceholder =
    class
    end

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
    /// 主指令起始符
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

    let mutable ImageBackgroundColor = System.Drawing.Color.White
    let mutable ImageTextColor = System.Drawing.Color.Black
    let mutable RowBackgroundColorA = System.Drawing.Color.White
    let mutable RowBackgroundColorB = System.Drawing.Color.LightGray

    /// 调整字符显示宽度。如果IsMatch=true则认为是1栏宽
    ///
    /// 需要设置RegexOptions.Compiled
    let mutable CharDisplayLengthAdj =
        Regex(@"\p{IsBasicLatin}|\p{IsGeneralPunctuation}|±", RegexOptions.Compiled)

    [<RequireQualifiedAccess>]
    module TextTable =
        let mutable CellPadding = "--"

        /// 计算字符宽度
        let inline CharLen (c) =
            if CharDisplayLengthAdj.IsMatch(c.ToString()) then 1 else 2

        /// 计算字符串宽度
        let inline StrDispLen (str : string) =
            str.ToCharArray() |> Array.sumBy CharLen

        [<Literal>]
        let HalfWidthSpace = ' '

        [<Literal>]
        let FullWidthSpace = '　'
