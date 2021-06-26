namespace KPX.FsCqHttp.Api.Private

open KPX.FsCqHttp
open KPX.FsCqHttp.Api


/// 获取陌生人信息
type GetStrangerInfo(userId : UserId, ?noCache : bool) =
    inherit CqHttpApiBase("get_stranger_info")

    let noCache = defaultArg noCache false

    member val UserId = UserId 0UL with get, set
    member val NickName = "" with get, set
    member val Sex = "" with get, set
    member val Age = 0 with get, set

    override x.WriteParams(w, js) =
        w.WritePropertyName("user_id")
        js.Serialize(w, userId)
        w.WritePropertyName("no_cache")
        w.WriteValue(noCache)

    override x.HandleResponse(r) =
        x.UserId <- r.Data.["user_id"] |> uint64 |> UserId
        x.NickName <- r.Data.["nickname"]
        x.Sex <- r.Data.["sex"]
        x.Age <- r.Data.["age"] |> int32
