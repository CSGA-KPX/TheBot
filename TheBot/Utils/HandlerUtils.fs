module TheBot.Utils.HandlerUtils

open System
open System.Collections
open KPX.FsCqHttp.Handler.CommandHandlerBase

let private admins = [| 313388419L; 343512452L |] |> Set.ofArray

/// 检查发信人是不是管理员
let isSenderAdmin (msgArg : CommandArgs)  = 
    admins.Contains(msgArg.MessageEvent.UserId)

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