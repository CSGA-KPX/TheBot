namespace KPX.FsCqHttp.Event

open System

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open KPX.FsCqHttp.Event.Request


[<JsonConverter(typeof<RequestEventConverter>)>]
type RequestEvent =
    | FriendRequest of FriendRequestEvent
    | GroupRequest of GroupRequestEvent

and RequestEventConverter() =
    inherit JsonConverter<RequestEvent>()

    override x.WriteJson(_ : JsonWriter, _ : RequestEvent, _ : JsonSerializer) =
        raise<unit> <| NotImplementedException()

    override x.ReadJson(r : JsonReader, _ : Type, _ : RequestEvent, _ : bool, _ : JsonSerializer) =
        let obj = JObject.Load(r)

        match obj.["request_type"].Value<string>() with
        | "friend" -> FriendRequest(obj.ToObject<FriendRequestEvent>())
        | "group" -> GroupRequest(obj.ToObject<GroupRequestEvent>())
        | other ->
            NLog
                .LogManager
                .GetCurrentClassLogger()
                .Fatal("未知请求类型：{0}", other)

            raise<RequestEvent>
            <| ArgumentOutOfRangeException()
