namespace rec KPX.FsCqHttp.Event.Message

open Newtonsoft.Json


[<CLIMutable>]
/// 群消息发送者类型
type GroupSender =
    { [<JsonProperty("nickname")>]
      Nickname: string
      /// male/female/unknown
      [<JsonProperty("sex")>]
      Sex: string
      [<JsonProperty("age")>]
      Age: int
      ///群消息用：群名片／备注
      [<JsonProperty("card")>]
      Card: string
      ///群消息用：地区
      [<JsonProperty("area")>]
      Area: string
      ///群消息用：成员等级
      [<JsonProperty("level")>]
      Level: string
      ///群消息用：角色
      /// owner/admin/member
      [<JsonProperty("role")>]
      Role: GroupSenderRole
      ///群消息用：专属头衔
      [<JsonProperty("title")>]
      Title: string }
