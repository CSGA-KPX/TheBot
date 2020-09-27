namespace KPX.FsCqHttp.Api.GroupApi

open KPX.FsCqHttp.DataType.Message
open KPX.FsCqHttp.DataType.Response
open KPX.FsCqHttp.Api

type SetGroupKick() =
    inherit ApiRequestBase("set_group_kick")

    do raise <| System.NotImplementedException()

type SetGroupBan() =
    inherit ApiRequestBase("set_group_ban")

    do raise <| System.NotImplementedException()

type SetGroupAnonymousBan() =
    inherit ApiRequestBase("set_group_anonymous_ban")

    do raise <| System.NotImplementedException()

type SetGroupWholeBan() =
    inherit ApiRequestBase("set_group_whole_ban")

    do raise <| System.NotImplementedException()

type SetGroupAdmin() =
    inherit ApiRequestBase("set_group_admin")

    do raise <| System.NotImplementedException()

type SetGroupAnonymous() =
    inherit ApiRequestBase("set_group_anonymous")

    do raise <| System.NotImplementedException()

type SetGroupCard() =
    inherit ApiRequestBase("set_group_card")

    do raise <| System.NotImplementedException()

type SetGroupLeave() =
    inherit ApiRequestBase("set_group_leave")

    do raise <| System.NotImplementedException()

type SetGroupSpecialTitle() =
    inherit ApiRequestBase("set_group_special_title")

    do raise <| System.NotImplementedException()

type SetDiscussLeave() =
    inherit ApiRequestBase("set_discuss_leave")

    do raise <| System.NotImplementedException()

type GetGroupMemberInfo(groupId : uint64, userId : uint64, ?noCache : bool) =
    inherit ApiRequestBase("get_group_member_info")
    let noCache = defaultArg noCache false

    member val GroupId = 0UL with get, set
    member val UserId = 0UL with get, set
    member val NickName = "" with get, set
    member val Card = "" with get, set
    member val Sex = "" with get, set
    member val Age = 0 with get, set
    member val Area = "" with get, set
    member val JoinTime = 0UL with get, set
    member val LastSentTime = 0UL with get, set
    member val Level = "" with get, set
    member val Role = "" with get, set
    member val Unfriendly = false with get, set
    member val Title = "" with get, set
    member val TitleExpireTime = 0UL with get, set
    member val CardChangeable = false with get, set

    member x.DisplayName =
        if System.String.IsNullOrEmpty(x.Card) then x.NickName
        else x.Card

    override x.WriteParams(w, js) =
        w.WritePropertyName("group_id")
        w.WriteValue(groupId)

        w.WritePropertyName("user_id")
        w.WriteValue(userId)

        w.WritePropertyName("no_cache")
        w.WriteValue(noCache)

    override x.HandleResponse(r) =
        x.GroupId <- r.Data.["group_id"] |> uint64
        x.UserId <- r.Data.["user_id"] |> uint64
        x.NickName <- r.Data.["nickname"]
        x.Card <- r.Data.["card"]
        x.Sex <- r.Data.["sex"]
        x.Age <- r.Data.["age"] |> int32
        x.Area <- r.Data.["area"]

        x.JoinTime <- r.Data.["join_time"] |> uint64
        x.LastSentTime <- r.Data.["last_sent_time"] |> uint64

        x.Level <- r.Data.["level"]
        x.Role <- r.Data.["role"]

        x.Unfriendly <- r.Data.["unfriendly"] = "true"
        x.Title <- r.Data.["title"]
        x.TitleExpireTime <- r.Data.["title_expire_time"] |> uint64
        x.CardChangeable <- r.Data.["card_changeable"] = "true"

type GetGroupMemberList() =
    inherit ApiRequestBase("get_group_member_list")

    do raise <| System.NotImplementedException()
