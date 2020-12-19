namespace KPX.FsCqHttp.Event.Request

open System

open Newtonsoft.Json
open Newtonsoft.Json.Linq


type FriendRequestEvent =
    { [<JsonProperty("user_id")>]
      UserId : uint64
      [<JsonProperty("comment")>]
      Comment : string
      [<JsonProperty("flag")>]
      Flag : string }

type GroupRequestEvent =
    { [<JsonProperty("sub_type")>]
      SubType : string
      [<JsonProperty("group_id")>]
      GroupId : uint64
      [<JsonProperty("user_id")>]
      UserId : uint64
      [<JsonProperty("comment")>]
      Comment : string
      [<JsonProperty("flag")>]
      Flag : string }

    ///是否加群
    member x.IsAdd = x.SubType = "add"

    ///是否邀请
    member x.IsInvite = x.SubType = "invite"
