namespace KPX.FsCqHttp.Api.Private

open KPX.FsCqHttp.Api


/// 获取陌生人信息
type GetStrangerInfo(userid : uint64, ?noCache : bool) =
    inherit ApiRequestBase("get_stranger_info")

    let noCache = defaultArg noCache false

    member val UserId = 0UL with get, set
    member val NickName = "" with get, set
    member val Sex = "" with get, set
    member val Age = 0 with get, set

    override x.WriteParams(w, _) =
        w.WritePropertyName("user_id")
        w.WriteValue(userid)
        w.WritePropertyName("no_cache")
        w.WriteValue(noCache)

    override x.HandleResponse(r) =
        x.UserId <- r.Data.["user_id"] |> uint64
        x.NickName <- r.Data.["nickname"]
        x.Sex <- r.Data.["sex"]
        x.Age <- r.Data.["age"] |> int32
