namespace KPX.FsCqHttp.Event

open System

open Newtonsoft.Json

open KPX.FsCqHttp.Message


[<JsonConverter(typeof<EventResponseConverter>)>]
/// 快速操作类型
type EventResponse =
    | EmptyResponse
    | PrivateMessageResponse of reply: ReadOnlyMessage
    | GroupMessageResponse of
        reply: ReadOnlyMessage *
        at_sender: bool *
        delete: bool *
        kick: bool *
        ban: bool *
        ban_duration: int
    | FriendAddResponse of approve: bool * remark: string
    | GroupAddResponse of approve: bool * reason: string

and EventResponseConverter() =
    inherit JsonConverter<EventResponse>()

    override x.WriteJson(w: JsonWriter, r: EventResponse, js: JsonSerializer) =
        for prop in r.GetType().GetProperties() do
            if Char.IsLower(prop.Name.[0]) then
                w.WritePropertyName(prop.Name)
                js.Serialize(w, prop.GetValue(r))

    override x.ReadJson(_: JsonReader, _: Type, _: EventResponse, _: bool, _: JsonSerializer) =
        raise <| NotImplementedException()
