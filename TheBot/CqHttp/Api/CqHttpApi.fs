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
                            printfn "%s %s" p.Name (p.Value.ToString())
                            yield (p.Name, p.Value.ToString())
                |] |> readOnlyDict
        }

[<AbstractClass>]
type ApiRequestBase(action : string) as x =
    let p = new Dictionary<string, string>()
    let logger = NLog.LogManager.GetLogger(x.GetType().Name)

    member internal x.Logger = logger

    member x.RequestJson =
        let sb = new StringBuilder()
        let sw = new StringWriter(sb)
        let js = new JsonSerializer()
        use w = new JsonTextWriter(sw, Formatting = Formatting.Indented)
        w.WriteStartObject()
        w.WritePropertyName("action")
        w.WriteValue(action)
        for item in p do
            w.WritePropertyName(item.Key)
            w.WriteValue(item.Value)
        x.AddCustomeProperty(w, js)
        w.WriteEndObject()
        sb.ToString()

    abstract HandleResponseData : IReadOnlyDictionary<string, string> -> unit

    abstract AddCustomeProperty : JsonTextWriter * JsonSerializer-> unit

type QuickOperation(context : string) =
    inherit ApiRequestBase(".handle_quick_operation")

    member val Reply = KPX.TheBot.WebSocket.DataType.Response.EmptyResponse with get, set

    override x.AddCustomeProperty(w, js) =
        w.WritePropertyName("params")
        w.WriteStartObject()
        w.WritePropertyName("context")
        w.WriteRawValue(context)
        w.WritePropertyName("operation")
        w.WriteStartObject()
        js.Serialize(w, x.Reply)
        w.WriteEndObject()

        w.WriteEndObject()

    override x.HandleResponseData(r) = ()