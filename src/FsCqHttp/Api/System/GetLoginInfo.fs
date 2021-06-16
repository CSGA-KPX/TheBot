namespace KPX.FsCqHttp.Api.System

open KPX.FsCqHttp
open KPX.FsCqHttp.Api


/// 获取登录号信息
type GetLoginInfo() =
    inherit CqHttpApiBase("get_login_info")

    let mutable data = [||] |> readOnlyDict

    member x.UserId = data.["user_id"] |> uint64 |> UserId

    member x.Nickname = data.["nickname"]

    override x.HandleResponse(r) = data <- r.Data
