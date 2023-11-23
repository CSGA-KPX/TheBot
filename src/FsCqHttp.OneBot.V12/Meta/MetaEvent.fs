namespace KPX.FsCqHttp.OneBot.V12.Meta

open KPX.FsCqHttp.OneBot.V12


[<EventType("meta", "connect", "")>]
type ConnectEvent = { Version: string }

[<EventType("meta", "heartbeat", "")>]
type HeartBeatEvent = { Interval: int64 }

type BotInfo = { Self: BotSelf; Online: bool }

type OnebotStatus = { Good: bool; Bots: BotInfo[] }

[<EventType("meta", "status_update ", "")>]
//todo 需要联动GetStatus动作
type StatusUpdateEvent = { Status: OnebotStatus }