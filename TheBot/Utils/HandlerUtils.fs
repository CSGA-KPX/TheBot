module TheBot.Utils.HandlerUtils

open KPX.FsCqHttp.Handler.CommandHandlerBase

open TheBot.Utils.Config

let setOwner(userId : uint64) =
    ConfigManager.SystemConfig.Put("BotOwner", userId)

let getOwner() =
    let ret = ConfigManager.SystemConfig.Get("BotOwner", 0UL)
    if ret = 0UL then None else Some ret

/// 检查发信人是不是管理员
let isSenderOwner (msgArg : CommandArgs)  = 
    getOwner()
    |> Option.map (fun uid -> uid = msgArg.MessageEvent.UserId)
    |> Option.defaultValue false

let addAdmin(userId : uint64) = 
    let key = sprintf "IsAdmin:%i"userId
    ConfigManager.SystemConfig.Put(key, true)

/// 检查发信人是不是管理员
let isSenderAdmin (msgArg : CommandArgs)  = 
    let key = sprintf "IsAdmin:%i" msgArg.MessageEvent.UserId
    ConfigManager.SystemConfig.Get(key, false)

/// 检查发信人是不是管理员
/// 如果不是，则抛异常
let failOnNonAdmin (msgArg : CommandArgs) = 
    if not <| isSenderAdmin(msgArg) then
        failwith "朋友你不是狗管理"

/// 检查群消息发送者有没有管理权限（群主/群管理）
/// 非群消息返回false
let canSenderAdmin (msgArg : CommandArgs) = 
    if msgArg.MessageEvent.IsGroup then
        msgArg.MessageEvent.Sender.CanAdmin
    else
        false