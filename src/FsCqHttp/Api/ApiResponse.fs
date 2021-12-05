namespace KPX.FsCqHttp.Api

open System
open System.Collections.Generic

open Newtonsoft.Json
open Newtonsoft.Json.Linq


type ApiRetCode =
    | OK = 0
    | Async = 1
    | BadArgument = 100
    | InvalidData = 102
    | InvalidOperation = 103
    | RemoteAuthFailed = 104
    | AsyncFailed = 201
    | Http400 = 1400
    | Http401 = 1401
    | Http403 = 1403
    | Http404 = 1404

type ApiRetType =
    | Object
    | Array
    | Null

[<JsonConverter(typeof<ApiResponseConverter>)>]
type ApiResponse =
    { Status: string
      ReturnCode: ApiRetCode
      DataType: ApiRetType
      Data: IReadOnlyDictionary<string, string>
      Echo: string }

    member x.TryParseArrayData<'T>() =
        let json = x.Data.[ApiResponse.ArrayDataKey]
        JArray.Parse(json).ToObject<'T []>()

    static member internal ArrayDataKey = "ArrayData"

and ApiResponseConverter() =
    inherit JsonConverter<ApiResponse>()

    override x.WriteJson(_: JsonWriter, _: ApiResponse, _: JsonSerializer) =
        raise<unit> <| NotImplementedException()

    override x.ReadJson(r: JsonReader, _: Type, _: ApiResponse, _: bool, _: JsonSerializer) =
        let obj = JObject.Load(r)

        { Status = obj.["status"].Value<string>()
          ReturnCode = enum<ApiRetCode> (obj.["retcode"].Value<int32>())
          DataType =
              match obj.["data"].Type with
              | JTokenType.Array -> Array
              | JTokenType.Object -> Object
              | JTokenType.Null -> Null
              | _ -> failwithf "不应该有其他类型"
          Data =
              [| if obj.["data"].HasValues then
                     match obj.["data"].Type with
                     | JTokenType.Array -> yield (ApiResponse.ArrayDataKey, obj.["data"].ToString())
                     | JTokenType.Object ->
                         let child = obj.["data"].Value<JObject>()

                         for p in child.Properties() do
                             yield (p.Name, p.Value.ToString())
                     | JTokenType.Null -> ()
                     | _ -> failwithf "不应该有其他类型" |]
              |> readOnlyDict
          Echo = obj.["echo"].Value<string>() }
