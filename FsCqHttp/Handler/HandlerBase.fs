namespace KPX.FsCqHttp.Handler

open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Api

[<AbstractClass>]
type HandlerModuleBase(shared : bool) as x =

    new () = HandlerModuleBase(true)

    member x.IsSharedModule = shared

    member val ApiCaller : IApiCallProvider option = None with get, set

    member val Logger = NLog.LogManager.GetLogger(x.GetType().Name)

    abstract HandleMessage : ClientEventArgs * Event.Message.MessageEvent -> unit
    abstract HandleRequest : ClientEventArgs * Event.Request.RequestEvent -> unit
    abstract HandleNotice : ClientEventArgs * Event.Notice.NoticeEvent -> unit

    default x.HandleMessage(_, _) = ()
    default x.HandleRequest(_, _) = ()
    default x.HandleNotice(_, _) = ()

    abstract HandleCqHttpEvent : obj -> ClientEventArgs -> unit
    default x.HandleCqHttpEvent _ args =
        try
            match args.Event with
            | Event.EventUnion.Message y -> x.HandleMessage(args, y)
            | Event.EventUnion.Request y -> x.HandleRequest(args, y)
            | Event.EventUnion.Notice y -> x.HandleNotice(args, y)
            | _ -> ()
        with
        | e -> x.Logger.Fatal(sprintf "HandlerModuleBase捕获异常:%O" e)