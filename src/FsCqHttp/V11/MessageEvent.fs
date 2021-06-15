namespace rec KPX.FsCqHttp.Event.Test

open System

open KPX.FsCqHttp
open KPX.FsCqHttp.Event

open Newtonsoft.Json

[<CLIMutable>]
type PrivateSender =
    { [<JsonProperty("user_id")>]
      UserId : UserId
      [<JsonProperty("nickname")>]
      Nickname : string
      [<JsonProperty("sex")>]
      Sex : string
      [<JsonProperty("age")>]
      Age : int }
    
[<Struct>]
[<JsonConverter(typeof<SingleCaseInlineConverter<GroupMessageSubtype>>)>]
type PrivateMessageSubtype =
    /// 好友消息
    | Friend
    /// 群临时消息
    | Group
    /// 其他消息
    | Other
    
[<Struct>]
[<JsonConverter(typeof<SingleCaseInlineConverter<GroupSenderRole>>)>]
type GroupSenderRole =
    /// 群主
    | Owner
    /// 群管
    | Admin
    /// 群成员
    | Member
    
[<Struct>]
[<JsonConverter(typeof<SingleCaseInlineConverter<GroupMessageSubtype>>)>]
type GroupMessageSubtype =
    /// 正常消息
    | Normal
    /// 匿名消息
    | Anonymous
    /// 系统提示
    | Notice
    
[<CLIMutable>]
type GroupSender =
    { [<JsonProperty("user_id")>]
      UserId : UserId
      [<JsonProperty("nickname")>]
      Nickname : string
      /// male/female/unknown
      [<JsonProperty("sex")>]
      Sex : string
      [<JsonProperty("age")>]
      Age : int
      ///群消息用：群名片／备注
      [<JsonProperty("card")>]
      Card : string
      ///群消息用：地区
      [<JsonProperty("area")>]
      Area : string
      ///群消息用：成员等级
      [<JsonProperty("level")>]
      Level : string
      ///群消息用：角色
      /// owner/admin/member
      [<JsonProperty("role")>]
      Role : GroupSenderRole
      ///群消息用：专属头衔
      [<JsonProperty("title")>]
      Title : string }
    
type MessageEvent () =
    member val Time = 0L with get, set
    member val SelfId = UserId with get, set
    
    member val MessageId = MessageId.Zero with get, set
    member val UserId = UserId.Zero with get, set
    member val Message = Message.Message() with get,set
    member val FontColor = 0 with get, set
    
type PrivateMessageEvent() =
    inherit MessageEvent()
    
    
    