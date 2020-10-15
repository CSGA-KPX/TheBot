namespace KPX.FsCqHttp.DataType.Event.Request

open System
open Newtonsoft.Json
open Newtonsoft.Json.Linq

type FriendRequestEvent =
    { [<JsonProperty("user_id")>]
      UserId : uint64
      [<JsonProperty("comment")>]
      Comment : string
      [<JsonProperty("flag")>]
      Flag : string }

type GroupRequestEvent =
    { [<JsonProperty("sub_type")>]
      SubType : string
      [<JsonProperty("group_id")>]
      GroupId : uint64
      [<JsonProperty("user_id")>]
      UserId : uint64
      [<JsonProperty("comment")>]
      Comment : string
      [<JsonProperty("flag")>]
      Flag : string }

    ///是否加群
    member x.IsAdd = x.SubType = "add"

    ///是否邀请
    member x.IsInvite = x.SubType = "invite"

[<JsonConverter(typeof<RequestEventConverter>)>]
type RequestEvent =
    | FriendRequest of FriendRequestEvent
    | GroupRequest of GroupRequestEvent

and RequestEventConverter() =
    inherit JsonConverter<RequestEvent>()

    override x.WriteJson(_ : JsonWriter, _ : RequestEvent, _ : JsonSerializer) =
        raise<unit> <| NotImplementedException()

    override x.ReadJson(r : JsonReader, _ : Type, _ : RequestEvent, _ : bool,
                        _ : JsonSerializer) =
        let obj = JObject.Load(r)

        match obj.["request_type"].Value<string>() with
        | "friend" -> FriendRequest(obj.ToObject<FriendRequestEvent>())
        | "group" -> GroupRequest(obj.ToObject<GroupRequestEvent>())
        | other ->
            NLog.LogManager.GetCurrentClassLogger().Fatal("未知请求类型：{0}", other)
            raise<RequestEvent> <| ArgumentOutOfRangeException()
