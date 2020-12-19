namespace KPX.FsCqHttp.Handler

open System.Reflection

open KPX.FsCqHttp.Event

[<AbstractClass>]
type HandlerModuleBase() as x =
    static let allModules =
        [| yield! Assembly.GetExecutingAssembly().GetTypes()
           yield! Assembly.GetEntryAssembly().GetTypes() |]
        |> Array.filter
            (fun t ->
                t.IsSubclassOf(typeof<HandlerModuleBase>)
                && (not <| t.IsAbstract))

    static member AllDefinedModules = allModules

    member val Logger = NLog.LogManager.GetLogger(x.GetType().Name)

    abstract HandleMessage : CqEventArgs * MessageEvent -> unit
    abstract HandleRequest : CqEventArgs * RequestEvent -> unit
    abstract HandleNotice : CqEventArgs * NoticeEvent -> unit

    default x.HandleMessage(_, _) = ()
    default x.HandleRequest(_, _) = ()
    default x.HandleNotice(_, _) = ()

    abstract HandleCqHttpEvent : CqEventArgs -> unit

    default x.HandleCqHttpEvent(args) =
        match args.Event with
        | MessageEvent y -> x.HandleMessage(args, y)
        | RequestEvent y -> x.HandleRequest(args, y)
        | NoticeEvent y -> x.HandleNotice(args, y)
        | MetaEvent _ -> ()
