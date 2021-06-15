namespace KPX.FsCqHttp.Event.Request

open KPX.FsCqHttp
open KPX.FsCqHttp.Event

open Newtonsoft.Json


type FriendRequestEvent =
    { [<JsonProperty("user_id")>]
      UserId : UserId
      [<JsonProperty("comment")>]
      Comment : string
      [<JsonProperty("flag")>]
      Flag : string }

    member x.Response(approve : bool, ?remark : string) =
        FriendAddResponse(approve, (defaultArg remark ""))
