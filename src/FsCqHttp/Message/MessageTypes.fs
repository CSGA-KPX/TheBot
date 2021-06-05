namespace KPX.FsCqHttp.Message


[<RequireQualifiedAccess>]
type AtUserType =
    | All
    | User of uint64

    override x.ToString() =
        match x with
        | All -> "all"
        | User x -> x |> string

    /// 将CQ码中字符串转换为AtUserType
    static member internal FromString(str : string) =
        if str = "all" then All else User(str |> uint64)
