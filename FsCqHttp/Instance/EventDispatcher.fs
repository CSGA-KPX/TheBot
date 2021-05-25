namespace KPX.FsCqHttp.Instance

open System
open System.Collections.Generic

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Instance


type private TaskSchedulerMessage =
    | Task of ContextModuleInfo * CqEventArgs
    | Finished

[<RequireQualifiedAccess>]
module internal TaskScheduler =
    let private logger = NLog.LogManager.GetLogger("TaskScheduler")

    let rec private getRootExn (exn : exn) =
        if isNull exn.InnerException then
            exn
        else
            getRootExn exn.InnerException

    let private maxConcurrentCommands = Environment.ProcessorCount

    let private handleEvent (mi : ContextModuleInfo, args : CqEventArgs) =
        try
            match args with
            | :? CqMetaEventArgs as args ->
                for c in mi.MetaCallbacks do
                    c args
            | :? CqNoticeEventArgs as args ->
                for c in mi.NoticeCallbacks do
                    c args
            | :? CqRequestEventArgs as args ->
                for c in mi.RequestCallbacks do
                    c args
            | :? CqMessageEventArgs as args ->
                match mi.TryCommand(args) with
                | None ->
                    for c in mi.MessageCallbacks do
                        c args
                | Some ci ->
                    let cmdArgs =
                        CommandEventArgs(args, ci.CommandAttribute)

                    if KPX.FsCqHttp.Config.Logging.LogCommandCall then
                        args.Logger.Info(
                            "Calling handler {0}\r\n Command Context {1}",
                            ci.MethodName,
                            $"%A{args.Event}"
                        )

                    ci.MethodAction.Invoke(cmdArgs)
            | _ -> invalidArg "args" $"HandleEvent: 未知事件类型:%s{args.GetType().FullName}"

        with e ->
            let rootExn = getRootExn e

            match rootExn with
            | :? IgnoreException -> ()
            | :? ModuleException as me ->
                if args :? CqMessageEventArgs then
                    (args :?> CqMessageEventArgs)
                        .Reply $"错误：%s{me.Message}"

                args.Logger.Warn(
                    "[{0}] -> {1} : {2} \r\n ctx： {3}",
                    args.BotUserId,
                    me.ErrorLevel,
                    args.RawEvent,
                    me.Message
                )
            | _ ->
                if args :? CqMessageEventArgs then
                    (args :?> CqMessageEventArgs)
                        .Reply $"内部错误：%s{rootExn.Message}"

                args.Logger.Error $"捕获异常：\r\n {e}"

    let private agent =
        MailboxProcessor.Start
            (fun inbox ->
                async {
                    let queue = Queue<_>()
                    let count = ref 0

                    while true do
                        let! msg = inbox.Receive()

                        match msg with
                        | Task (ctx, args) -> queue.Enqueue((ctx, args))
                        | Finished -> decr count

                        if !count < maxConcurrentCommands && queue.Count > 0 then
                            incr count
                            let work = queue.Dequeue()

                            async {
                                handleEvent work
                                inbox.Post(Finished)
                            }
                            |> Async.Start

                        if !count >= maxConcurrentCommands && queue.Count > 0 then
                            logger.Warn("队列已满，当前并发：{0}，队列数：{1}。", !count, queue.Count)
                })

    let enqueue (ctx, args) = agent.Post(Task(ctx, args))
