namespace KPX.FsCqHttp.Api.System

open KPX.FsCqHttp.Api


/// 获取插件运行状态
type GetStatus() =
    inherit CqHttpApiBase("get_status")

    let mutable data = [||] |> readOnlyDict<string, string>

    member x.Online = data.["online"] |> System.Boolean.Parse
    member x.Good = data.["good"] |> System.Boolean.Parse

    override x.HandleResponse(r) = data <- r.Data
