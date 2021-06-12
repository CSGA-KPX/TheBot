namespace KPX.FsCqHttp.Handler

open KPX.FsCqHttp
open KPX.FsCqHttp.Event

open KPX.FsCqHttp.Api

open KPX.FsCqHttp.Handler


[<Sealed>]
type EventContext =
    class
        new : ctx: Newtonsoft.Json.Linq.JObject -> EventContext
        /// 懒惰求值的字符串
        override ToString : unit -> string
        member Event : Newtonsoft.Json.Linq.JObject
    end

and CqEventArgs =
    class
        static member Parse : api: IApiCallProvider * ctx: EventContext -> CqEventArgs
        static member Parse : api: IApiCallProvider * eventJson: string -> CqEventArgs
        /// 中断执行过程
        member Abort : level: ErrorLevel * fmt: string * [<System.ParamArray>] args: obj [] -> 'T
        member Reply : r: EventResponse -> unit
        member ApiCaller : IApiCallProvider
        member BotId : string
        member BotNickname : string
        member BotUserId : uint64
        member internal Logger : NLog.Logger
        member RawEvent : EventContext
    end

and CqMessageEventArgs =
    class
        inherit CqEventArgs
        new : api: IApiCallProvider * ctx: EventContext * e: MessageEvent -> CqMessageEventArgs
        member Abort : level: ErrorLevel * fmt: string * [<System.ParamArray>] args: obj [] -> 'T
        member Reply : str: string * ?atUser: bool -> unit
        member Reply : msg: Message.Message * ?atUser: bool -> unit
        member Event : MessageEvent
    end

and CqNoticeEventArgs =
    class
        inherit CqEventArgs
        new : api: IApiCallProvider * ctx: EventContext * e: NoticeEvent -> CqNoticeEventArgs
        member Event : NoticeEvent
    end

and CqRequestEventArgs =
    class
        inherit CqEventArgs
        new : api: IApiCallProvider * ctx: EventContext * e: RequestEvent -> CqRequestEventArgs
        member Event : RequestEvent
    end

and CqMetaEventArgs =
    class
        inherit CqEventArgs
        new : api: IApiCallProvider * ctx: EventContext * e: MetaEvent -> CqMetaEventArgs
        member Event : MetaEvent
    end
