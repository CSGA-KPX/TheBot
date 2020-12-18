namespace KPX.FsCqHttp.Api.Group

open KPX.FsCqHttp.Api


type GroupInfo =
    { [<Newtonsoft.Json.JsonProperty("group_id")>]
      GroupId : uint64
      [<Newtonsoft.Json.JsonProperty("group_name")>]
      GroupName : string }

/// 获取群列表
type GetGroupList() =
    inherit ApiRequestBase("get_group_list")

    member val Groups : GroupInfo [] = [||] with get, set

    override x.HandleResponse(r) = x.Groups <- r.TryParseArrayData<GroupInfo>()