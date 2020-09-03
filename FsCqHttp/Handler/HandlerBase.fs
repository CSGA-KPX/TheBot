﻿namespace KPX.FsCqHttp.Handler

open System.Reflection

open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Api

[<AbstractClass>]
type HandlerModuleBase(shared : bool) as x =
    static let allModules = 
        [| yield! Assembly.GetExecutingAssembly().GetTypes()
           yield! Assembly.GetEntryAssembly().GetTypes() |]
        |> Array.filter (fun t -> t.IsSubclassOf(typeof<HandlerModuleBase>) && (not <| t.IsAbstract))

    /// 声明类为共享模块
    new () = HandlerModuleBase(true)

    static member AllDefinedModules = allModules

    /// 指示该模块是否可以被多个账户共享，与线程安全等相关
    ///
    /// 如果为false，则每个链接会使用自己的实例
    member x.IsSharedModule = shared

    member val Logger = NLog.LogManager.GetLogger(x.GetType().Name)

    abstract HandleMessage : ClientEventArgs * Event.Message.MessageEvent -> unit
    abstract HandleRequest : ClientEventArgs * Event.Request.RequestEvent -> unit
    abstract HandleNotice : ClientEventArgs * Event.Notice.NoticeEvent -> unit

    default x.HandleMessage(_, _) = ()
    default x.HandleRequest(_, _) = ()
    default x.HandleNotice(_, _) = ()

    abstract HandleCqHttpEvent : ClientEventArgs -> unit
    default x.HandleCqHttpEvent(args)=
        try
            match args.Event with
            | Event.EventUnion.Message y -> x.HandleMessage(args, y)
            | Event.EventUnion.Request y -> x.HandleRequest(args, y)
            | Event.EventUnion.Notice y -> x.HandleNotice(args, y)
            | _ -> ()
        with
        | e -> 
            if not (e.InnerException :? KPX.FsCqHttp.Handler.RuntimeHelpers.IgnoreException) then
                x.Logger.Fatal(sprintf "HandlerModuleBase捕获异常:%O" e)