namespace rec KPX.FsCqHttp

open System

open SkiaSharp

open KPX.FsCqHttp.Utils.UserOption
open KPX.FsCqHttp.Instance


type private ColorCell(cb, name, def) =
    inherit OptionCell<SKColor>(cb, name, def)

    override x.ConvertValue(str) = SKColor.Parse(str)

type FsCqHttpConfigParser() as x =
    inherit OptionBase()

    do x.UndefinedOptionHandling <- UndefinedOptionHandling.Ignore

    let logEventPost = OptionCellSimple<bool>(x, "LogEventPost", Config.LogEventPost)

    let logApiCall = OptionCellSimple<bool>(x, "LogApiCall", Config.LogApiCall)

    let logApiJson = OptionCellSimple<bool>(x, "LogApiJson", Config.LogApiJson)

    let logCommandCall = OptionCellSimple<bool>(x, "LogCommandCall", Config.LogCommandCall)

    let newLine = OptionCellSimple<string>(x, "NewLine", Config.NewLine)

    let textLengthLimit = OptionCellSimple<int>(x, "TextLengthLimit", Config.TextLengthLimit)

    let imgIgnoreCheck = OptionCellSimple<bool>(x, "ImageIgnoreSendCheck", Config.ImageIgnoreSendCheck)

    let imgOutputFont = OptionCellSimple<string>(x, "ImageOutputFont", Config.ImageOutputFont)

    let imgOutputSize = OptionCellSimple<float32>(x, "ImageOutputSize", Config.ImageOutputSize)

    let imgTextColor = ColorCell(x, "ImageTextColor", Config.ImageTextColor)

    let imgRowBgColorA = ColorCell(x, "ImageRowColorA", Config.ImageRowColorA)

    let imgRowBgColorB = ColorCell(x, "ImageRowColorB", Config.ImageRowColorB)

    let tblCellPadding = OptionCellSimple<string>(x, "TableCellPadding", Config.TableCellPadding)

    let tblGraphicMeasure = OptionCellSimple<bool>(x, "TableGraphicMeasure", Config.TableGraphicMeasure)

    let endpoint = OptionCellSimple<string>(x, "endpoint", "")

    let token = OptionCellSimple<string>(x, "token", "")

    let reverse = OptionCellSimple<int>(x, "reverse", 5004)

    member private x.UpdateConfig() = Config <- FsCqHttpConfig(x)

    member private x.GetStartupFunction() =
        if reverse.IsDefined && token.IsDefined then
            (fun () ->
                let endpoint = $"http://localhost:%i{reverse.Value}/"

                let wss = new CqWebSocketServer(endpoint, token.Value)

                wss.Start())
        elif endpoint.IsDefined && token.IsDefined then
            (fun () ->
                let uri = Uri(endpoint.Value)
                let token = token.Value
                let aws = ActiveWebsocket(uri, token)
                let ctx = aws.GetContext()
                CqWsContextPool.Instance.AddContext(ctx))
        else
            failwithf "需要定义endpoint&token或者reverse&token"

    /// <summary>
    /// 使用自定义的ContextModuleLoader启动
    /// </summary>
    member x.Start(loader: ContextModuleLoader) =
        CqWsContextPool.ContextModuleLoader <- loader
        x.UpdateConfig()
        x.GetStartupFunction() ()

    /// <summary>
    /// 使用LoadedAssemblyDiscover启动
    /// </summary>
    member x.Start() =
        x.Start(ContextModuleLoader(LoadedAssemblyDiscover().AllDefinedModules))

    /// 从环境变量读取配置
    member x.ParseEnvironment() =
        seq {
            let vars = Environment.GetEnvironmentVariables() |> Seq.cast<Collections.DictionaryEntry>

            for kv in vars do
                yield $"{string kv.Key}:{string kv.Value}"
        }
        |> x.Parse

    interface IFsCqHttpConfig with
        member x.LogEventPost = logEventPost.Value
        member x.LogApiCall = logApiCall.Value
        member x.LogApiJson = logApiJson.Value
        member x.LogCommandCall = logCommandCall.Value

        member x.NewLine = newLine.Value
        member x.TextLengthLimit = textLengthLimit.Value

        member x.ImageIgnoreSendCheck = imgIgnoreCheck.Value
        member x.ImageOutputFont = imgOutputFont.Value
        member x.ImageOutputSize = imgOutputSize.Value
        member x.ImageTextColor = imgTextColor.Value
        member x.ImageRowColorA = imgRowBgColorA.Value
        member x.ImageRowColorB = imgRowBgColorB.Value
        member x.TableCellPadding = tblCellPadding.Value
        member x.TableGraphicMeasure = tblGraphicMeasure.Value
