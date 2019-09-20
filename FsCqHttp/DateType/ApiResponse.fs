namespace KPX.FsCqHttp.DataType.Response
open System
open System.Collections.Generic
open Newtonsoft.Json
open Newtonsoft.Json.Linq

type ApiRetCode =
    | OK                = 0
    | Async             = 1
    | BadArgument       = 100
    | InvalidData       = 102
    | InvalidOperation  = 103
    | RemoteAuthFailed  = 104
    | AsyncFailed       = 201
    | Http400           = 1400
    | Http401           = 1401
    | Http403           = 1403
    | Http404           = 1404

[<JsonConverter(typeof<ApiResponseConverter>)>]
type ApiResponse =
    {
        Status     : string
        ReturnCode : ApiRetCode
        Data       : IReadOnlyDictionary<string, string>
        Echo       : string
    }
and ApiResponseConverter() =
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