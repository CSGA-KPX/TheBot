namespace rec KPX.FsCqHttp.Utils.AuthLevel


[<RequireQualifiedAccess>]
type AuthLevel =
    | Banned
    | Guest
    | GroupAdmin
    | GroupOwner
    | BotAdmin
    | InstanceAdmin
