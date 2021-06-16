namespace rec KPX.FsCqHttp.Event.Message

open Newtonsoft.Json


[<CLIMutable>]
type PrivateSender =
    { [<JsonProperty("nickname")>]
      Nickname : string
      [<JsonProperty("sex")>]
      Sex : string
      [<JsonProperty("age")>]
      Age : int }