[<AutoOpen>]
module KPX.FsCqHttp.Utils.TextResponse.Extension

open KPX.FsCqHttp.Handler


type CqMessageEventArgs with

    /// 生成一个强制文本输出的TextResponse
    member x.OpenResponse() = new TextResponse(x, ForceText)

    /// 生成一个按指定ResponseType输出的TextResponse
    member x.OpenResponse(respType : ResponseType) = new TextResponse(x, respType)
