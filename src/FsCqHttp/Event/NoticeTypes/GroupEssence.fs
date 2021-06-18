namespace KPX.FsCqHttp.Event.Notice

open Newtonsoft.Json

open KPX.FsCqHttp


[<Struct>]
[<JsonConverter(typeof<StringEnumConverter<EssenceSubType>>)>]
type EssenceSubType =
    /// 添加到精华
    | Add
    /// 移出精华
    | Delete

type GroupEssence =
    { [<JsonProperty("sub_type")>]
      SubType : EssenceSubType
      [<JsonProperty("sender_id")>]
      SenderId : UserId
      [<JsonProperty("operator_id")>]
      OperatorId : UserId
      [<JsonProperty("message_id")>]
      MessageId : MessageId }
