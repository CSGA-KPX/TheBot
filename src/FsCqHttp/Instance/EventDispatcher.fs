namespace KPX.FsCqHttp.Instance

open System
open System.Collections.Generic

open KPX.FsCqHttp
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Instance


[<RequireQualifiedAccess>]
[<Struct>]
type internal TaskContext =
    | Meta of meta: CqMetaEventArgs
    | Notice of notice: CqNoticeEventArgs
    | Request of request: CqRequestEventArgs
    | Command of cmd: CommandEventArgs * info: CommandInfo
    | Message of msg: CqMessageEventArgs

type private TaskSchedulerMessage =
    | Task of ContextModuleInfo * TaskContext
    | Finished

[<RequireQualifiedAccess>]
module internal TaskScheduler =
    let private logger = NLog.LogManager.GetLogger("TaskScheduler")

    let rec private getRootExn (exn: exn) =
        if isNull exn.InnerException then
            exn
        else
            getRootExn exn.InnerException

    let private maxConcurrentCommands = Environment.ProcessorCount

    let private handleEvent (mi: ContextModuleInfo, task: TaskContext) =
        try

            match task with
            | TaskContext.Meta args ->
                for c in mi.MetaCallbacks do
                    c args
            | TaskContext.Notice args ->
                for c in mi.NoticeCallbacks do
                    c args
            | TaskContext.Request args ->
                for c in mi.RequestCallbacks do
                    c args
            | TaskContext.Command (args, ci) ->
                if Config.LogCommandCall then
                    args.Logger.Info("Calling handler {0}\r\n Command Context {1}", ci.MethodName, $"%A{args.Event}")

                match ci.MethodAction with
                | ManualAction action -> action.Invoke(args)
                | AutoAction func -> func.Invoke(args).Response(args)
            | TaskContext.Message args ->
                for c in mi.MessageCallbacks do
                    c args
        with
        | e ->
            let args: CqEventArgs =
                match task with
                | TaskContext.Meta args -> args
                | TaskContext.Notice args -> args
                | TaskContext.Request args -> args
                | TaskContext.Message args -> args
                | TaskContext.Command (args, _) -> args

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

    /// <summary>
    /// 将任务装入调度器
    /// </summary>
    /// <param name="ctx">模块环境</param>
    /// <param name="args">事件</param>
    let enqueue (ctx, args) = agent.Post(Task(ctx, args))
