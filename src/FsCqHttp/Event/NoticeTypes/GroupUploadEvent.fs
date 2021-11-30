namespace KPX.FsCqHttp.Event.Notice

open Newtonsoft.Json

open KPX.FsCqHttp


type GroupUploadEvent =
    { [<JsonProperty("group_id")>]
      GroupId: GroupId
      [<JsonProperty("user_id")>]
      UserId: UserId
      [<JsonProperty("file")>]
      File: GroupFile }
