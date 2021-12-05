namespace KPX.FsCqHttp

open SkiaSharp


/// 用于传递配置信息
/// 具体注释请参阅KPX.FsCqHttp.FsCqHttpConfig
type IFsCqHttpConfig =
    abstract LogEventPost: bool
    abstract LogApiCall: bool
    abstract LogApiJson: bool
    abstract LogCommandCall: bool

    abstract NewLine: string
    abstract TextLengthLimit: int32

    abstract ImageIgnoreSendCheck: bool
    abstract ImageOutputFont: string
    abstract ImageOutputSize: float32
    abstract ImageTextColor: SKColor
    abstract ImageRowColorA: SKColor
    abstract ImageRowColorB: SKColor

    abstract TableCellPadding: string
    abstract TableGraphicMeasure: bool

/// 从IFsCqHttpConfig复制信息并缓存
/// 并提供读写支持
type FsCqHttpConfig(cfg: IFsCqHttpConfig) =
    /// 是否记录事件上报
    member val LogEventPost = cfg.LogEventPost with get, set
    /// 是否记录API调用
    member val LogApiCall = cfg.LogApiCall with get, set
    /// API调用记录中是否包含JSON请求
    member val LogApiJson = cfg.LogApiJson with get, set
    /// 是否记录指令事件
    member val LogCommandCall = cfg.LogCommandCall with get, set

    /// 输出换行符
    /// 影响go-cqhttp分片
    member val NewLine = cfg.NewLine with get, set

    /// 文本模式下最长字符数
    member val TextLengthLimit = cfg.TextLengthLimit with get, set

    /// 是否跳过CanSendImage API检查。
    /// 可以减少一次API调用时间
    member val ImageIgnoreSendCheck = cfg.ImageIgnoreSendCheck with get, set
    /// 图片输出下使用的字体，请使用等宽字体
    /// Mono上GDI+处理和Windows不同，字体渲染存在差异
    // 目前已知Sarasa Fixed CL字体输出比较稳定，其他不明
    member val ImageOutputFont = cfg.ImageOutputFont with get, set
    /// 图片输出时的字号
    member val ImageOutputSize = cfg.ImageOutputSize with get, set
    /// 图片输出时文字颜色
    member val ImageTextColor = cfg.ImageTextColor with get, set
    /// 图片输出交替行颜色
    member val ImageRowColorA = cfg.ImageRowColorA with get, set
    /// 图片输出交替行颜色
    member val ImageRowColorB = cfg.ImageRowColorB with get, set

    /// TextTable单元格默认文本
    member val TableCellPadding = cfg.TableCellPadding with get, set
    /// 是否使用Graphics.MeasureString计算字符串长度，
    /// 否则使用正则匹配。推荐True
    member val TableGraphicMeasure = cfg.TableGraphicMeasure with get, set

    member x.HalfWidthSpace = ' '

    member x.FullWidthSpace = '　'

[<AutoOpen>]
module ConfigInstance =
    let mutable Config =
        { new IFsCqHttpConfig with
            member x.LogEventPost = false
            member x.LogApiCall = false
            member x.LogApiJson = false
            member x.LogCommandCall = true

            member x.NewLine = "\r"
            member x.TextLengthLimit = 3000

            member x.ImageIgnoreSendCheck = true
            member x.ImageOutputFont = "Sarasa Fixed CL"
            member x.ImageOutputSize = 12.0f
            member x.ImageTextColor = SKColors.Black
            member x.ImageRowColorA = SKColors.White
            member x.ImageRowColorB = SKColors.LightGray

            member x.TableCellPadding = "--"
            member x.TableGraphicMeasure = true }
        |> FsCqHttpConfig
