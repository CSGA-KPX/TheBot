namespace KPX.FsCqHttp.DataType.Event

open System
open Newtonsoft.Json.Linq

type CqHttpEvent =
    | Notice of Notice.NoticeEvent
    | Message of Message.MessageEvent
    | Request of Request.RequestEvent
    | Meta of Meta.MetaEvent

    static member FromJObject(x : JObject) =
        match x.["post_type"].Value<string>() with
        | "message" -> Message(x.ToObject<Message.MessageEvent>())
        | "notice" -> Notice(x.ToObject<Notice.NoticeEvent>())
        | "request" -> Request(x.ToObject<Request.RequestEvent>())
        | "meta_event" -> Meta(Meta.MetaEvent.FromJObject(x))
        | other -> raise <| ArgumentException("未知上报类型：" + other)
