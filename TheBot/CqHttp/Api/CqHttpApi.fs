namespace KPX.TheBot.WebSocket.Api
open System
open System.IO
open System.Collections.Generic
open System.Text
open Newtonsoft.Json
open Newtonsoft.Json.Linq

type ApiRetCode =
    | OK    = 0
    | Async = 1
    | BadArgument = 100
    | InvalidData = 102
    | InvalidOperation = 103
    | RemoteAuthFailed = 104
    | AsyncFailed      = 201
    | Http400 = 1400
    | Http401 = 1401
    | Http403 = 1403
    | Http404 = 1404

[<JsonConverter(typeof<ApiResponse_Converter>)>]
type ApiResponse =
    {
        Status     : string
        ReturnCode : ApiRetCode
        Data       : IReadOnlyDictionary<string, string>
        Echo       : string
    }
and ApiResponse_Converter() =
    inherit JsonConverter<ApiResponse>()

    override x.WriteJson(w:JsonWriter , r : ApiResponse, s:JsonSerializer) =
        raise<unit> <| NotImplementedException()

    override x.ReadJson(r : JsonReader, objType : Type, existingValue : ApiResponse, hasExistingValue : bool, s : JsonSerializer) =
        let obj = JObject.Load(r)
        {
            Status = obj.["status"].Value<string>()
            ReturnCode = enum<ApiRetCode>(obj.["retcode"].Value<int32>())
            Data =
                [|
                    if obj.["data"].HasValues then
                        let child = obj.["data"].Value<JObject>()
                        for p in child.Properties() do
                            yield (p.Name, p.Value.ToString())
                |] |> readOnlyDict
            Echo = obj.["echo"].Value<string>()
        }

[<AbstractClass>]
type ApiRequestBase(action : string) as x =
    let logger = NLog.LogManager.GetLogger(x.GetType().Name)

    member internal x.Logger = logger

    member x.GetRequestJson(?echo : string) =
        let echo = defaultArg echo ""
        let sb = new StringBuilder()
        let sw = new StringWriter(sb)
        let js = new JsonSerializer()
        use w = new JsonTextWriter(sw, Formatting = Formatting.Indented)
        w.WriteStartObject()
        if not <| String.IsNullOrEmpty(echo) then
            w.WritePropertyName("echo")
            w.WriteValue(echo)

        w.WritePropertyName("action")
        w.WriteValue(action)
        w.WritePropertyName("params")
        w.WriteStartObject()
        x.WriteParams(w, js)
        w.WriteEndObject()
        w.WriteEndObject()
        sb.ToString()

    abstract HandleResponseData : IReadOnlyDictionary<string, string> -> unit

    abstract WriteParams : JsonTextWriter * JsonSerializer-> unit

    default x.HandleResponseData _ = ()
    default x.WriteParams(_,_) = ()

    override x.ToString() = 
        let sb = new StringBuilder()
        let props = x.GetType().GetProperties(Reflection.BindingFlags.Instance ||| Reflection.BindingFlags.Public ||| Reflection.BindingFlags.DeclaredOnly)
        let header = sprintf "%s---" (x.GetType().Name)
        sb.AppendLine(header)  |> ignore
        for prop in props do 
            sb.AppendFormat("{0} => {1}\r\n", prop.Name, prop.GetValue(x)) |> ignore
        sb.AppendLine("".PadRight(header.Length, '-'))  |> ignore
        sb.ToString()

type QuickOperation(context : string) =
    inherit ApiRequestBase(".handle_quick_operation")

    member val Reply = KPX.TheBot.WebSocket.DataType.Response.EmptyResponse with get, set

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
