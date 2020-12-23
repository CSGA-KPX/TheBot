namespace KPX.FsCqHttp.Instance

open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading
open System.Net.WebSockets

open KPX.FsCqHttp.Event
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.Api.System
open KPX.FsCqHttp.Handler

open Newtonsoft.Json.Linq


type CqWsContext(ws : WebSocket) =
    static let moduleCache = Dictionary<Type, HandlerModuleBase>()
    let cts = new CancellationTokenSource()
    let utf8 = Text.Encoding.UTF8
    let logger = NLog.LogManager.GetCurrentClassLogger()

    let apiPending =
        ConcurrentDictionary<string, ManualResetEvent * ApiRequestBase>()

    let started = new ManualResetEvent(false)
    let modules = List<HandlerModuleBase>()
    let cmdCache = Dictionary<string, _>()
    let self = GetLoginInfo()

    let rec getRootExn (exn : exn) =
        if isNull exn.InnerException then exn else getRootExn exn.InnerException

    /// 发生链接错误时重启服务器的函数
    ///
    /// 值为None时，不再尝试重连
    member val RestartContext : (unit -> CqWsContext) option = None with get, set

    /// 获取登录号信息
    member x.Self = self

    /// 获取登录号标识符
    member x.SelfId =
        if self.IsExecuted then sprintf "[%i:%s]" self.UserId self.Nickname else "[--:--]"

    /// 注册模块
    member x.RegisterModule(t : Type) =
        let toAdd =
            if moduleCache.ContainsKey(t) then
                moduleCache.[t]
            else
                let isSubClass =
                    t.IsSubclassOf(typeof<HandlerModuleBase>)
                    && (not <| t.IsAbstract)

                if not isSubClass then invalidArg "t" "必须是HandlerModuleBase子类"

                let m =
                    Activator.CreateInstance(t) :?> HandlerModuleBase

                moduleCache.Add(t, m)
                m

        modules.Add(toAdd)

        if toAdd :? CommandHandlerBase then
            let cmdBase = toAdd :?> CommandHandlerBase

            for (str, attr, method) in cmdBase.Commands do
                cmdCache.Add(str, (toAdd, attr, method))

    /// 使用API检查是否在线
    member x.CheckOnline() =
        if ws.State <> WebSocketState.Open then
            false
        else
            try
                let ret =
                    (x :> IApiCallProvider).CallApi<GetStatus>()

                ret.Online && ret.Good
            with _ -> false

    /// 启用消息循环
    member x.Start() =
        if cts.IsCancellationRequested then
            logger.Fatal("无法启动消息循环：已调用Stop或Ws异常终止")
            invalidOp "CancellationRequested"

        if ws.State <> WebSocketState.Open then
            logger.Fatal("无法启动消息循环：WebSocketState状态检查失败。")
            logger.Fatal(sprintf "%A %A %A" ws.State ws.CloseStatus ws.CloseStatusDescription)
            cts.Cancel()
            invalidOp "WebSocketState <> Open"

        x.StartMessageLoop()
        started.WaitOne() |> ignore

        (x :> IApiCallProvider).CallApi(self) |> ignore

    member x.Stop() = cts.Cancel()

    member private x.HandleEventPost(args : CqEventArgs) =
        try
            match args.Event with
            | MetaEvent _ -> () // 心跳和生命周期事件没啥用
            | NoticeEvent e ->
                for m in modules do
                    m.HandleNotice(args, e)
            | RequestEvent e ->
                for m in modules do
                    m.HandleRequest(args, e)
            | MessageEvent e ->
                let str = e.Message.ToString()

                let endIdx =
                    let idx = str.IndexOf(" ")
                    if idx = -1 then str.Length else idx
                // 空格-1，msg.Length变换为idx也需要-1
                let key = str.[0..endIdx - 1].ToLowerInvariant()
                // 如果和指令有匹配就直接走模块
                // 没匹配再轮询
                if cmdCache.ContainsKey(key) then
                    let (cmdModule, attr, method) = cmdCache.[key]
                    let cmdArg = CommandEventArgs(args, e, attr)

                    if KPX.FsCqHttp.Config.Logging.LogCommandCall then
                        logger.Info("Calling handler {0}\r\n Command Context {1}", method.Name, sprintf "%A" e)
                        method.Invoke(cmdModule, [| cmdArg |]) |> ignore
                else
                    for m in modules do
                        m.HandleMessage(args, e)

        with e ->
            let rootExn = getRootExn e

            match rootExn with
            | :? IgnoreException -> ()
            | :? ModuleException as me ->
                args.Logger.Warn(
                    "[{0}] -> {1} : {2} \r\n ctx： {3}",
                    x.SelfId,
                    me.ErrorLevel,
                    sprintf "%A" args.Event,
                    me.Message
                )

                args.QuickMessageReply(sprintf "错误：%s" me.Message)
                args.AbortExecution(me.ErrorLevel, me.Message)
            | _ ->
                args.QuickMessageReply(sprintf "内部错误：%s" rootExn.Message)
                logger.Error(sprintf "捕获异常：\r\n %O" e)

    member private x.HandleApiResponse(ret : ApiResponse) =
        let hasPending, item = apiPending.TryGetValue(ret.Echo)

        if hasPending then
            let (mre, api) = item
            api.HandleResponse(ret)
            mre.Set() |> ignore
        else
            logger.Warn(sprintf "未注册echo:%s" ret.Echo)

    member private x.HandleMessage(json : string) =
        let obj = JObject.Parse(json)

        let logJson = lazy (obj.ToString())

        if (obj.ContainsKey("post_type")) then //消息上报
            if KPX.FsCqHttp.Config.Logging.LogEventPost then logger.Trace(sprintf "%s收到上报：%s" x.SelfId logJson.Value)

            x.HandleEventPost(CqEventArgs(x, obj))

        elif obj.ContainsKey("retcode") then //API调用结果
            if KPX.FsCqHttp.Config.Logging.LogApiCall
            then logger.Trace(sprintf "%s收到API调用结果： %s" x.SelfId logJson.Value)

            x.HandleApiResponse(obj.ToObject<ApiResponse>())

    member private x.StartMessageLoop() =
        async {
            let buffer = Array.zeroCreate<byte> 4096
            let seg = ArraySegment<byte>(buffer)
            use ms = new IO.MemoryStream()

            let rec readMessage (ms : IO.MemoryStream) =
                let s =
                    ws.ReceiveAsync(seg, cts.Token)
                    |> Async.AwaitTask
                    |> Async.RunSynchronously

                ms.Write(seg.Array, seg.Offset, s.Count)
                if s.EndOfMessage then utf8.GetString(ms.ToArray()) else readMessage (ms)

            try
                started.Set() |> ignore

                while (not cts.IsCancellationRequested) do
                    ms.SetLength(0L)
                    let json = readMessage (ms)

                    Tasks
                        .Task
                        .Run((fun () -> x.HandleMessage(json)))
                        .ContinueWith(fun t ->
                            if t.IsFaulted then
                                for inner in t.Exception.InnerExceptions do
                                    logger.Fatal(sprintf "捕获异常%s : %s" (inner.GetType().Name) inner.Message))
                    |> ignore
            with e ->
                cts.Cancel()
                logger.Fatal(sprintf "%sWS读取捕获异常：%A" x.SelfId e)
                CqWsContextPool.Instance.RemoveContext(x)

                if x.RestartContext.IsSome then
                    logger.Warn(sprintf "%s正在尝试重新连接" x.SelfId)
                    CqWsContextPool.Instance.RemoveContext(x.RestartContext.Value())
        }
        |> Async.Start

    interface IApiCallProvider with
        member x.CallApi(req) =
            started.WaitOne() |> ignore

            async {
                let echo = Guid.NewGuid().ToString()
                let mre = new ManualResetEvent(false)
                let json = req.GetRequestJson(echo)

                let _ = apiPending.TryAdd(echo, (mre, req :> ApiRequestBase))

                if KPX.FsCqHttp.Config.Logging.LogApiCall then
                    logger.Trace(sprintf "%s请求API：%s" x.SelfId req.ActionName)
                    if KPX.FsCqHttp.Config.Logging.LogApiJson then logger.Trace(sprintf "%s请求API：%s" x.SelfId json)

                let data = json |> utf8.GetBytes

                do!
                    ws.SendAsync(ArraySegment<byte>(data), WebSocketMessageType.Text, true, cts.Token)
                    |> Async.AwaitTask

                let! _ = Async.AwaitWaitHandle(mre :> WaitHandle)

                apiPending.TryRemove(echo) |> ignore

                req.IsExecuted <- true
                return req
            }
            |> Async.RunSynchronously

        member x.CallApi<'T when 'T :> ApiRequestBase and 'T : (new : unit -> 'T)>() =
            let req = Activator.CreateInstance<'T>()
            (x :> IApiCallProvider).CallApi(req)

    interface IDisposable with
        member x.Dispose() = x.Stop()

and CqWsContextPool private () =
    static let instance = CqWsContextPool()
    let logger = NLog.LogManager.GetCurrentClassLogger()

    let pool =
        ConcurrentDictionary<uint64, CqWsContext>()

    member x.AddContext(context : CqWsContext) =
        pool.TryAdd(context.Self.UserId, context)
        |> ignore

        logger.Info(sprintf "已接受连接:%s" context.SelfId)

    member x.RemoveContext(context : CqWsContext) =
        pool.TryRemove(context.Self.UserId) |> ignore
        logger.Info(sprintf "已移除连接:%s" context.SelfId)

    interface Collections.IEnumerable with
        member x.GetEnumerator() =
            pool.Values.GetEnumerator() :> Collections.IEnumerator

    interface Collections.Generic.IEnumerable<CqWsContext> with
        member x.GetEnumerator() = pool.Values.GetEnumerator()

    static member Instance : CqWsContextPool = instance
