namespace rec KPX.FsCqHttp.Event.Message

open Newtonsoft.Json

open KPX.FsCqHttp


[<Struct>]
[<JsonConverter(typeof<StringEnumConverter<GroupSenderRole>>)>]
type GroupSenderRole =
    /// 群主
    | Owner
    /// 群管
    | Admin
    /// 群成员
    | Member
