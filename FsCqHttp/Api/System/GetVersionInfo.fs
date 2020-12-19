namespace KPX.FsCqHttp.Api.System

open KPX.FsCqHttp.Api


/// 获取 酷Q 及 HTTP API 插件的版本信息
type GetVersionInfo() =
    inherit ApiRequestBase("get_version_info")

    let mutable data = [||] |> readOnlyDict

    member x.CoolqDirectory = data.["coolq_directory"]
    member x.CoolqEdition = data.["coolq_edition"]
    member x.PluginVersion = data.["plugin_version"]
    member x.PluginBuildNumber = data.["plugin_build_number"]
    member x.PluginBuildConfiguration = data.["plugin_build_configuration"]

    override x.HandleResponse(r) = data <- r.Data
