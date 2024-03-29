﻿namespace KPX.FsCqHttp.Api.Group

open KPX.FsCqHttp
open KPX.FsCqHttp.Api


type GetGroupMemberInfo(groupId: GroupId, userId: UserId, ?noCache: bool) =
    inherit CqHttpApiBase("get_group_member_info")
    let noCache = defaultArg noCache false

    member val GroupId = GroupId 0UL with get, set
    member val UserId = UserId 0UL with get, set
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
        if System.String.IsNullOrEmpty(x.Card) then
            x.NickName
        else
            x.Card

    override x.WriteParams(w, js) =
        w.WritePropertyName("group_id")
        js.Serialize(w, groupId)

        w.WritePropertyName("user_id")
        js.Serialize(w, userId)

        w.WritePropertyName("no_cache")
        w.WriteValue(noCache)

    override x.HandleResponse(r) =
        x.GroupId <- r.Data.["group_id"] |> uint64 |> GroupId
        x.UserId <- r.Data.["user_id"] |> uint64 |> UserId
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
