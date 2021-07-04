namespace rec KPX.FsCqHttp.Event.Message

open Newtonsoft.Json

open KPX.FsCqHttp


[<Struct>]
[<JsonConverter(typeof<StringEnumConverter<GroupMessageSubtype>>)>]
/// 群信息子类型
type GroupMessageSubtype =
    /// 正常消息
    | Normal
    /// 匿名消息
    | Anonymous
    /// 系统提示
    | Notice