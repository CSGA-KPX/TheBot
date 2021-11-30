namespace KPX.FsCqHttp.Message

open KPX.FsCqHttp


[<RequireQualifiedAccess>]
type AtUserType =
    | All
    | User of UserId

    override x.ToString() =
        match x with
        | All -> "all"
        | User x -> x |> string

    /// 将CQ码中字符串转换为AtUserType
    static member internal FromString(str: string) =
        if str = "all" then
            All
        else
            User(str |> uint64 |> UserId)
