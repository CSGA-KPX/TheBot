namespace KPX.FsCqHttp.Event.Message

open Newtonsoft.Json


/// 发送人信息
///
/// 各字段是尽最大努力提供的，也就是说，不保证每个字段都一定存在，也不保证存在的字段都是完全正确的（缓存可能过期）。
[<CLIMutable>]
type Sender =
    { [<JsonProperty("user_id")>]
      UserId : uint64
      [<JsonProperty("nickname")>]
      NickName : string
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
      Role : string
      ///群消息用：专属头衔
      [<JsonProperty("title")>]
      Title : string }

    /// 发信人是不是群主
    /// 请先检查是否是群消息
    member x.IsOwner = x.Role = "owner"

    /// 发信人是不是管理员
    /// 请先检查是否是群消息
    member x.IsAdmin = x.Role = "admin"

    /// 发信人是不是群成员
    /// 请先检查是否是群消息
    member x.IsMember = x.Role = "member"

    /// 发信人有没有管理权限（群主/管理员）
    /// 请先检查是否是群消息
    member x.CanAdmin = x.IsOwner || x.IsAdmin

[<CLIMutable>]
type AnonymousUser =
    { /// 匿名用户 ID
      [<JsonProperty("id")>]
      Id : uint64
      /// 匿名用户名称
      [<JsonProperty("name")>]
      Name : string
      /// 匿名用户 flag，在调用禁言 API 时需要传入
      [<JsonProperty("flag")>]
      Flag : string }
