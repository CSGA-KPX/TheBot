[<AutoOpen>]
module KPX.FsCqHttp.Utils.TextResponse.Extensions

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse


type CqMessageEventArgs with

    /// 生成一个按指定ResponseType输出的TextResponse
    member x.OpenResponse(respType: ResponseType) = new TextResponse(x, respType)

    member x.Reply(table: TextTable, respType: ResponseType) =
        table.PreferResponseType <- respType
        table.Response(x)
