namespace KPX.FsCqHttp.Handler

open KPX.FsCqHttp
open KPX.FsCqHttp.Event

open KPX.FsCqHttp.Api

open KPX.FsCqHttp.Handler


type CqEventArgs =
    class
        static member Parse : api: IApiCallProvider * ctx: PostContent -> CqEventArgs
        static member Parse : api: IApiCallProvider * eventJson: string -> CqEventArgs
        /// 中断执行过程
        member Abort : level: ErrorLevel * fmt: string * [<System.ParamArray>] args: obj [] -> 'T
        member Reply : r: EventResponse -> unit
        member ApiCaller : IApiCallProvider
        member BotId : string
        member BotNickname : string
        member BotUserId : UserId
        member internal Logger : NLog.Logger
        member RawEvent : PostContent
    end

and CqMessageEventArgs =
    class
        inherit CqEventArgs
        new : api: IApiCallProvider * ctx: PostContent * e: MessageEvent -> CqMessageEventArgs
        member Abort : level: ErrorLevel * fmt: string * [<System.ParamArray>] args: obj [] -> 'T
        member Reply : str: string -> unit
        member Reply : msg: Message.ReadOnlyMessage -> unit
        member Event : MessageEvent
    end

and CqNoticeEventArgs =
    class
        inherit CqEventArgs
        new : api: IApiCallProvider * ctx: PostContent * e: NoticeEvent -> CqNoticeEventArgs
        member Event : NoticeEvent
    end

and CqRequestEventArgs =
    class
        inherit CqEventArgs
        new : api: IApiCallProvider * ctx: PostContent * e: RequestEvent -> CqRequestEventArgs
        member Event : RequestEvent
    end

and CqMetaEventArgs =
    class
        inherit CqEventArgs
        new : api: IApiCallProvider * ctx: PostContent * e: MetaEvent -> CqMetaEventArgs
        member Event : MetaEvent
    end
