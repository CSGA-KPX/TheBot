namespace KPX.FsCqHttp.Event

open System
open Newtonsoft.Json.Linq


type CqHttpEvent =
    | NoticeEvent of NoticeEvent
    | MessageEvent of MessageEvent
    | RequestEvent of RequestEvent
    | MetaEvent of MetaEvent

    static member FromJObject(x : JObject) =
        match x.["post_type"].Value<string>() with
        | "message" -> MessageEvent(x.ToObject<MessageEvent>())
        | "notice" -> NoticeEvent(x.ToObject<NoticeEvent>())
        | "request" -> RequestEvent(x.ToObject<RequestEvent>())
        | "meta_event" -> MetaEvent(MetaEvent.FromJObject(x))
        | other -> raise <| ArgumentException("未知上报类型：" + other)
