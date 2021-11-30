namespace KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Handler


type TestFixtureAttribute() =
    inherit System.Attribute()

[<AbstractClass>]
type HandlerModuleBase() as x =
    member val Logger = NLog.LogManager.GetLogger(x.GetType().Name)

    /// 是否接受非指令消息。启用会导致所有非指令消息进入消息队列，影响性能
    abstract OnMessage: (CqMessageEventArgs -> unit) option
    abstract OnRequest: (CqRequestEventArgs -> unit) option
    abstract OnNotice: (CqNoticeEventArgs -> unit) option
    abstract OnMeta: (CqMetaEventArgs -> unit) option

    default x.OnMessage = None
    default x.OnRequest = None
    default x.OnNotice = None
    default x.OnMeta = None
