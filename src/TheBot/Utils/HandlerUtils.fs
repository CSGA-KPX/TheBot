module KPX.TheBot.Host.Utils.HandlerUtils

open System.Collections.Generic

open KPX.FsCqHttp
open KPX.FsCqHttp.Event
open KPX.FsCqHttp.Handler

open KPX.TheBot.Host.Utils.Config


[<Literal>]
let private instanceOwnerKey = "InstanceOwner"

let private botAdminKey (uid: UserId) = $"BotAdmins:%i{uid.Value}"

type CqEventArgs with

    member x.SetInstanceOwner(uid: UserId) =
        ConfigManager.SystemConfig.Put(instanceOwnerKey, uid)

    member x.GetBotAdmins() =
        ConfigManager.SystemConfig.Get(botAdminKey x.BotUserId, HashSet<uint64>())

    member x.GrantBotAdmin(uid: UserId) =
        let current = x.GetBotAdmins()
        current.Add(uid.Value) |> ignore
        ConfigManager.SystemConfig.Put(botAdminKey x.BotUserId, current)

type CommandEventArgs with

    /// 检查是否有群管理权限。包含群主或群管理员
    ///
    /// None 表示不是群消息事件
    member x.IsGroupAdmin =
        match x.MessageEvent with
        | MessageEvent.Private _ -> None
        | MessageEvent.Group g -> Some(g.Sender.Role.CanAdmin)

    member x.EnsureSenderOwner() =
        if not <| x.IsSenderOwner then
            failwith "需要超管权限"

    member x.IsSenderOwner =
        let ret = ConfigManager.SystemConfig.Get(instanceOwnerKey, 0UL)

        (if ret = 0UL then None else Some ret)
        |> Option.map (fun uid -> uid = x.MessageEvent.UserId.Value)
        |> Option.defaultValue false

    member x.EnsureSenderAdmin() =
        if not <| x.IsSenderAdmin then
            failwith "需要管理员权限"

    member x.IsSenderAdmin =
        x
            .GetBotAdmins()
            .Contains(x.MessageEvent.UserId.Value)
