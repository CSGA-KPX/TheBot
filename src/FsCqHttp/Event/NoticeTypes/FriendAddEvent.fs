namespace KPX.FsCqHttp.Event.Notice

open KPX.FsCqHttp

open Newtonsoft.Json


type FriendAddEvent =
    { [<JsonProperty("user_id")>]
      UserId : UserId }