namespace KPX.FsCqHttp.Api.SystemApi
open KPX.FsCqHttp.DataType.Response
open KPX.FsCqHttp.Api

/// 快速操作
type QuickOperation(context : string) =
    inherit ApiRequestBase(".handle_quick_operation")

    member val Reply = EmptyResponse with get, set

    override x.WriteParams(w, js) =
        w.WritePropertyName("context")
        w.WriteRawValue(context)
        w.WritePropertyName("operation")
        w.WriteStartObject()
        js.Serialize(w, x.Reply)
        w.WriteEndObject()


/// 获取登录号信息
type GetLoginInfo() = 
    inherit ApiRequestBase("get_login_info")

    let mutable data = [||] |> readOnlyDict

    member x.UserId = data.["user_id"] |> int64

    member x.Nickname = data.["nickname"]

    override x.HandleResponseData(r) = 
        data <- r

/// 获取插件运行状态 
type GetStatus() = 
    inherit ApiRequestBase("get_status")

    let mutable data = [||] |> readOnlyDict

    member x.Online = data.["online"]
    member x.Good   = data.["good"]

    override x.HandleResponseData(r) = 
        data <- r


/// 获取 酷Q 及 HTTP API 插件的版本信息 
type GetVersionInfo() = 
    inherit ApiRequestBase("get_version_info")

    let mutable data = [||] |> readOnlyDict

    member x.CoolqDirectory             = data.["coolq_directory"]
    member x.CoolqEdition               = data.["coolq_edition"]
    member x.PluginVersion              = data.["plugin_version"]
    member x.PluginBuildNumber          = data.["plugin_build_number"]
    member x.PluginBuildConfiguration   = data.["plugin_build_configuration"]

    override x.HandleResponseData(r) = 
        data <- r
