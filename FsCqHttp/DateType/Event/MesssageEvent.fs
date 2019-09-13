namespace KPX.FsCqHttp.DataType.Event.Message
open Newtonsoft.Json

/// 发送人信息
///
/// 各字段是尽最大努力提供的，也就是说，不保证每个字段都一定存在，也不保证存在的字段都是完全正确的（缓存可能过期）。
[<CLIMutable>]
type Sender =
    {
        [<JsonProperty("user_id")>]
        UserId      : int64
        [<JsonProperty("nickname")>]
        NickName    : string
        /// male/female/unknown
        [<JsonProperty("sex")>]
        Sex         : string
        [<JsonProperty("age")>]
        Age         : int
        ///群消息用：群名片／备注
        [<JsonProperty("card")>]
        Card         : string
        ///群消息用：地区
        [<JsonProperty("area")>]
        Area         : string
        ///群消息用：成员等级
        [<JsonProperty("level")>]
        Level         : string
        ///群消息用：角色
        /// owner/admin/member
        [<JsonProperty("role")>]
        Role         : string
        ///群消息用：专属头衔
        [<JsonProperty("title")>]
        Title         : string

    }

[<CLIMutable>]
type AnonymousUser =
    {
        /// 匿名用户 ID
        [<JsonProperty("id")>]
        Id   : int64
        /// 匿名用户名称
        [<JsonProperty("name")>]
        Name : string
        /// 匿名用户 flag，在调用禁言 API 时需要传入
        [<JsonProperty("flag")>]
        Flag : string
    }

[<CLIMutable>]
type MessageEvent =
    {
        [<JsonProperty("message_type")>]
        MessageType : string
        [<JsonProperty("sub_type")>]
        SubType     : string
        [<JsonProperty("message_id")>]
        MessageId   : int32
        [<JsonProperty("user_id")>]
        UserId      : int64
        [<JsonProperty("message")>]
        Message     : KPX.FsCqHttp.DataType.Message.Message
        [<JsonProperty("raw_message")>]
        RawMessage  : string
        [<JsonProperty("font")>]
        Font        : int32
        [<JsonProperty("sender")>]
        Sender      : Sender

        /// 群号
        /// 群消息专用
        [<JsonProperty("group_id")>]
        GroupId     : uint64
        /// 匿名信息，如果不是匿名消息则为 null
        /// 群消息专用
        [<JsonProperty("anonymous")>]
        Anonymous   : AnonymousUser

        /// 讨论组号
        /// 讨论组消息专用
        [<JsonProperty("discuss_id")>]
        DiscussId   : uint64
    }

    member x.IsPrivate = x.MessageType = "private"

    member x.IsGroup = x.MessageType = "group"

    member x.IsDiscuss = x.MessageType = "discuss"

    /// 获取用户名称，如果是群消息则获取群名片
    member x.GetNicknameOrCard = 
        match x with
        | x when x.IsPrivate -> x.Sender.NickName
        | x when x.IsDiscuss -> x.Sender.NickName
        | x when x.IsGroup   -> x.Sender.Card
        | _ -> failwithf ""