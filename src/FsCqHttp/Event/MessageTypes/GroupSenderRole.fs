namespace rec KPX.FsCqHttp.Event.Message

open Newtonsoft.Json

open KPX.FsCqHttp


[<Struct>]
[<JsonConverter(typeof<StringEnumConverter<GroupSenderRole>>)>]
/// 群消息事件发送者角色
type GroupSenderRole =
    /// 群主
    | Owner
    /// 群管
    | Admin
    /// 群成员
    | Member

    /// 该成员是否具有管理权限
    [<JsonIgnore>]
    member x.CanAdmin =
        match x with
        | Member -> false
        | _ -> true
