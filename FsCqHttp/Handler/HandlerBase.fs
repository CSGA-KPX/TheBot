namespace KPX.FsCqHttp.Handler

open System.Reflection

open KPX.FsCqHttp.DataType

[<AbstractClass>]
type HandlerModuleBase() as x =
    static let allModules = 
        [| yield! Assembly.GetExecutingAssembly().GetTypes()
           yield! Assembly.GetEntryAssembly().GetTypes() |]
        |> Array.filter (fun t -> t.IsSubclassOf(typeof<HandlerModuleBase>) && (not <| t.IsAbstract))

    static member AllDefinedModules = allModules

    member val Logger = NLog.LogManager.GetLogger(x.GetType().Name)

    abstract HandleMessage : ClientEventArgs * Event.Message.MessageEvent -> unit
    abstract HandleRequest : ClientEventArgs * Event.Request.RequestEvent -> unit
    abstract HandleNotice : ClientEventArgs * Event.Notice.NoticeEvent -> unit

    default x.HandleMessage(_, _) = ()
    default x.HandleRequest(_, _) = ()
    default x.HandleNotice(_, _) = ()

    abstract HandleCqHttpEvent : ClientEventArgs -> unit
    default x.HandleCqHttpEvent(args)=
        match args.Event with
        | Event.CqHttpEvent.Message y -> x.HandleMessage(args, y)
        | Event.CqHttpEvent.Request y -> x.HandleRequest(args, y)
        | Event.CqHttpEvent.Notice y -> x.HandleNotice(args, y)
        | Event.CqHttpEvent.Meta _ -> ()