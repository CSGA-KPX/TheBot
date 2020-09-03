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

    member x.UserId = data.["user_id"] |> uint64

    member x.Nickname = data.["nickname"]

    override x.HandleResponse(r) = data <- r.Data

/// 获取插件运行状态
type GetStatus() =
    inherit ApiRequestBase("get_status")

    let mutable data = [||] |> readOnlyDict

    member x.Online = data.["online"] |> System.Boolean.Parse
    member x.Good = data.["good"] |> System.Boolean.Parse

    override x.HandleResponse(r) = data <- r.Data

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

/// 获取陌生人信息
type GetStrangerInfo(userid : uint64, ?noCache : bool) =
    inherit ApiRequestBase("get_stranger_info")

    let noCache = defaultArg noCache false

    member val UserId = 0UL with get, set
    member val NickName = "" with get, set
    member val Sex = "" with get, set
    member val Age = 0 with get, set

    override x.WriteParams(w, js) =
        w.WritePropertyName("user_id")
        w.WriteValue(userid)
        w.WritePropertyName("no_cache")
        w.WriteValue(noCache)

    override x.HandleResponse(r) =
        x.UserId <- r.Data.["user_id"] |> uint64
        x.NickName <- r.Data.["nickname"]
        x.Sex <- r.Data.["sex"]
        x.Age <- r.Data.["age"] |> int32

type GroupInfo =
    { [<Newtonsoft.Json.JsonProperty("group_id")>]
      GroupId : uint64
      [<Newtonsoft.Json.JsonProperty("group_name")>]
      GroupName : string }

/// 获取群列表
type GetGroupList() =
    inherit ApiRequestBase("get_group_list")

    member val Groups : GroupInfo [] = [||] with get, set

    override x.HandleResponse(r) = x.Groups <- r.TryParseArrayData<GroupInfo>()

/// 检查是否可以发送图片
type CanSendImage() =
    inherit ApiRequestBase("can_send_image")

    member val Can = false with get, set

    override x.HandleResponse(r) = x.Can <- r.Data.["yes"] |> System.Boolean.Parse

/// 检查是否可以发送语音
type CanSendRecord() =
    inherit ApiRequestBase("can_send_record")

    member val Can = false with get, set

    override x.HandleResponse(r) = x.Can <- r.Data.["yes"]  |> System.Boolean.Parse
