namespace KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Event


[<AbstractClass>]
type HandlerModuleBase() as x =
    member val Logger = NLog.LogManager.GetLogger(x.GetType().Name)

    abstract HandleMessage : CqEventArgs * MessageEvent -> unit
    abstract HandleRequest : CqEventArgs * RequestEvent -> unit
    abstract HandleNotice : CqEventArgs * NoticeEvent -> unit

    default x.HandleMessage(_, _) = ()
    default x.HandleRequest(_, _) = ()
    default x.HandleNotice(_, _) = ()
