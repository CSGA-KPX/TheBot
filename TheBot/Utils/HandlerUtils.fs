module TheBot.Utils.HandlerUtils

open System.Collections.Generic

open KPX.FsCqHttp.Handler

open TheBot.Utils.Config

[<Literal>]
let private instanceOwnerKey = "InstanceOwner"

let private botAdminKey (uid : uint64) = sprintf "BotAdmins:%i" uid

type ClientEventArgs with

    member x.SetInstanceOwner(uid : uint64) = 
        ConfigManager.SystemConfig.Put(instanceOwnerKey, uid)

    member x.GetBotAdmins() =
        ConfigManager.SystemConfig.Get(botAdminKey x.SelfId, HashSet<uint64>())

    member x.GrantBotAdmin(uid : uint64) = 
        let current = x.GetBotAdmins()
        current.Add(uid) |> ignore
        ConfigManager.SystemConfig.Put(botAdminKey x.SelfId, current)

type CommandArgs with
    
    /// 检查是否有群管理权限。包含群主或群管理员
    ///
    /// None 表示不是群消息事件
    member x.IsGroupAdmin = 
        if x.MessageEvent.IsGroup then
            Some x.MessageEvent.Sender.CanAdmin
        else
            None

    member x.EnsureSenderOwner() = 
        if not <| x.IsSenderOwner then failwith "需要超管权限"

    member x.IsSenderOwner = 
        let ret = ConfigManager.SystemConfig.Get(instanceOwnerKey, 0UL)
        (if ret = 0UL then None else Some ret)
        |> Option.map (fun uid -> uid = x.MessageEvent.UserId)
        |> Option.defaultValue false

    member x.EnsureSenderAdmin() = 
        if not <| x.IsSenderAdmin then failwith "需要管理员权限"

    member x.IsSenderAdmin = 
        x.GetBotAdmins().Contains(x.MessageEvent.UserId)