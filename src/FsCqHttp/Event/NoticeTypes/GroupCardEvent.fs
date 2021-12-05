namespace KPX.FsCqHttp.Event.Notice

open Newtonsoft.Json

open KPX.FsCqHttp


/// 群名片更改事件
type GroupCardEvent =
    { [<JsonProperty("group_id")>]
      GroupId: GroupId
      [<JsonProperty("user_id")>]
      UserId: UserId
      /// 新名片
      [<JsonProperty("card_new")>]
      CardNew: string
      /// 旧名片
      [<JsonProperty("card_old")>]
      CardOld: string }
