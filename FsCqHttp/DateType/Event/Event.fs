namespace KPX.FsCqHttp.DataType.Event
open System
open Newtonsoft.Json.Linq

type EventUnion = 
    | Notice  of Notice.NoticeEvent
    | Message of Message.MessageEvent
    | Request of Request.RequestEvent
    /// 目前没有适用于WebSocket的元事件
    | Meta

    member private x.As<'T>() = 
        ()

    member x.AsMessageEvent = 
        match x with
        | Message x -> x
        | _ -> raise <| InvalidOperationException()

    member x.AsNoticeEvent = 
        match x with
        | Notice x -> x
        | _ -> raise <| InvalidOperationException()

    member x.AsRequestEvent = 
        match x with
        | Request x -> x
        | _ -> raise <| InvalidOperationException()

    static member From(x : JObject) = 
        match x.["post_type"].Value<string>() with
        | "message" ->
            Message (x.ToObject<Message.MessageEvent>())
        | "notice" ->
            Notice  (x.ToObject<Notice.NoticeEvent>())
        | "request" -> 
            Request (x.ToObject<Request.RequestEvent>())
        | "meta_event" ->
            Meta
        | other ->
            raise <| ArgumentException("未知上报类型：" + other)