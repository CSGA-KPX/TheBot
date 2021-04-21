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
    /// 是否记录API请求的Json
    let mutable LogApiJson = false
    /// 是否记录指令事件
    let mutable LogCommandCall = true

[<RequireQualifiedAccess>]
module Output =
    open System.Drawing

    /// 输出使用的换行符
    /// Windows QQ使用\r，Android使用\n
    /// iOS设备可能有识别问题
    let NewLine = "\r"

    /// 如果设置，将不再使用CanSendImage API检查上游功能
    let ForceImageAvailable = true

    /// 文本回复下最长输出字符数
    let TextLengthLimit = 3000

    /// 图片输出下使用的字体
    // Mono上GDI+处理和Windows不同，字体渲染存在差异
    // 目前已知Sarasa Fixed CL字体输出比较稳定，其他不明
    let ImageOutputFont = "Sarasa Fixed CL"
    let ImageOutputSize = 12.0f

    let ImageBackgroundColor = Color.White
    let ImageTextColor = Color.Black

    let RowBackgroundColorA = Color.White
    let RowBackgroundColorB = Color.LightGray

    [<RequireQualifiedAccess>]
    module TextTable =
        let mutable CellPadding = "--"

        /// 使用Graphics.MeasureString计算字符串长度，
        /// 否则使用正则匹配
        let UseGraphicStringMeasure = true

        [<Literal>]
        let HalfWidthSpace = ' '

        [<Literal>]
        let FullWidthSpace = '　'
