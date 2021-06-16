namespace rec KPX.FsCqHttp.Event.Message

open Newtonsoft.Json

open KPX.FsCqHttp


[<CLIMutable>]
type AnonymousUser =
    { /// 匿名用户 ID
      [<JsonProperty("id")>]
      Id : UserId
      /// 匿名用户名称
      [<JsonProperty("name")>]
      Name : string
      /// 匿名用户 flag，在调用禁言 API 时需要传入
      [<JsonProperty("flag")>]
      Flag : string }
