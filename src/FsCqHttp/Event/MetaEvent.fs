namespace KPX.FsCqHttp.Event

open System
open Newtonsoft.Json.Linq


type MetaEvent =
    /// WebSocket模式下不存在
    | PluginEnabled
    /// WebSocket模式下不存在
    | PluginDisabled
    | WebSocketConnected of SelfId : uint64 * TimeStamp : uint64
    /// 心跳事件
    ///
    /// Status字段实现情况不同。请通过get_status获取。
    | HeartBeat of SelfId : uint64 * TimeStamp : uint64 * Interval : uint64

    static member FromJObject(obj : JObject) =
        if not <| obj.ContainsKey("meta_event_type") then invalidArg "obj" "输入不是元事件"

        match obj.["meta_event_type"].Value<string>() with
        | "lifecycle" ->
            match obj.["sub_type"].Value<string>() with
            | "enable"
            | "disable" -> failwithf "Websocket连接不应出现Enable/Disable生命周期事件"
            | "connect" ->
                let sid = obj.["self_id"].Value<uint64>()
                let ts = obj.["time"].Value<uint64>()
                WebSocketConnected(sid, ts)
            | other -> raise <| ArgumentException("未知生命周期事件类型：" + other)
        | "heartbeat" ->
            let sid = obj.["self_id"].Value<uint64>()
            let ts = obj.["time"].Value<uint64>()
            let int = obj.["interval"].Value<uint64>()
            HeartBeat(sid, ts, int)
        | other -> raise <| ArgumentException("未知元事件类型：" + other)
