namespace rec KPX.FsCqHttp

open System
open System.Drawing

open KPX.FsCqHttp.Utils.UserOption
open KPX.FsCqHttp.Instance


type private ColorCell(cb, name, def) =
    inherit OptionCell<Color>(cb, name, def)

    override x.ConvertValue(str) = Color.FromName(str)

type FsCqHttpConfigParser() as x =
    inherit OptionBase()

    do x.UndefinedOptionHandling <- UndefinedOptionHandling.Ignore

    let logEventPost =
        OptionCellSimple<bool>(x, "LogEventPost", false)

    let logApiCall =
        OptionCellSimple<bool>(x, "LogApiCall", false)

    let logApiJson =
        OptionCellSimple<bool>(x, "LogApiJson", false)

    let logCommandCall =
        OptionCellSimple<bool>(x, "LogCommandCall", true)

    let newLine =
        OptionCellSimple<string>(x, "NewLine", "\r")

    let textLengthLimit =
        OptionCellSimple<int>(x, "TextLengthLimit", 3000)

    let imgIgnoreCheck =
        OptionCellSimple<bool>(x, "ImageIgnoreSendCheck", true)

    let imgOutputFont =
        OptionCellSimple<string>(x, "ImageOutputFont", "Sarasa Fixed CL")

    let imgOutputSize =
        OptionCellSimple<float32>(x, "ImageOutputSize", 12.0f)

    let imgTextColor =
        ColorCell(x, "ImageTextColor", Color.Black)

    let imgRowBgColorA =
        ColorCell(x, "ImageRowColorA", Color.White)

    let imgRowBgColorB =
        ColorCell(x, "ImageRowColorB", Color.LightGray)

    let tblCellPadding =
        OptionCellSimple<string>(x, "TableCellPadding", "--")

    let tblGraphicMeasure =
        OptionCellSimple<bool>(x, "TableGraphicMeasure", true)

    let endpoint =
        OptionCellSimple<string>(x, "endpoint", "")

    let token = OptionCellSimple<string>(x, "token", "")

    let reverse =
        OptionCellSimple<int>(x, "reverse", 5004)

    member private x.UpdateConfig() = Config <- FsCqHttpConfig(x)

    member private x.GetStartupFunction() =
        if reverse.IsDefined && token.IsDefined then
            (fun () ->
                let endpoint = $"http://localhost:%i{reverse.Value}/"

                let wss =
                    new CqWebSocketServer(endpoint, token.Value)

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

    member x.Start() =
        x.UpdateConfig()
        x.GetStartupFunction() ()

    /// 从环境变量读取配置
    member x.ParseEnvironment() =
        seq {
            let vars =
                Environment.GetEnvironmentVariables()
                |> Seq.cast<Collections.DictionaryEntry>

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
