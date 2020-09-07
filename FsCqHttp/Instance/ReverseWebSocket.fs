namespace KPX.FsCqHttp.Instance

open System
open System.Threading
open System.Net
open KPX.FsCqHttp.Handler

/// Reverse Websocket服务端
/// 
/// Start()后接受连接并提交给CqWsContextPool，
/// 尚未完成鉴权
type CqWebSocketServer(uriPrefix, token : string ) = 
    let logger = NLog.LogManager.GetCurrentClassLogger()

    let listener = new HttpListener()

    let mutable ctsListener = new CancellationTokenSource()
    let mutable isRunning = false
    do
        listener.Prefixes.Add(uriPrefix)

    member x.Start() =
        if isRunning then invalidOp "正在运行"
        ctsListener <- new CancellationTokenSource()
        logger.Info("Starting Reverse Websocket server.")
        listener.Start()
        x.StartListenConnection()
        isRunning <- true

    member x.Stop() = 
        logger.Info("Stopping Reverse Websocket server.")
        ctsListener.Cancel()
        listener.Stop()
        isRunning <- false


    member private x.StartListenConnection() = 
        async {
        while not ctsListener.Token.IsCancellationRequested do
            let! ctx = listener.GetContextAsync() |> Async.AwaitTask
            // Async()期间可能会被取消
            if ctsListener.Token.IsCancellationRequested then failwithf "已取消操作"

            let isWebSocketRequest = 
                if isNull (Type.GetType("Mono.Runtime")) then
                    ctx.Request.IsWebSocketRequest
                else
                    // Mono下IsWebSocketRequest始终返回false
                    // 只能简略处理
                    let hdrs = ctx.Request.Headers
                    hdrs.["Connection"] <> null
                    && hdrs.["Upgrade"] <> null
                    && hdrs.["Sec-WebSocket-Key"] <> null
                    && hdrs.["Sec-WebSocket-Version"] <> null
                    && hdrs.["Authorization"] <> null
                    && hdrs.["X-Client-Role"] <> null
                    && hdrs.["X-Self-ID"] <> null
                    && hdrs.["User-Agent"] <> null

            if not isWebSocketRequest then
                ctx.Response.StatusCode <- 403
                ctx.Response.Close()
            else
                try
                    let! wsCtx = ctx.AcceptWebSocketAsync(null) |> Async.AwaitTask

                    let auth = wsCtx.Headers.["Authorization"]
                    if not (auth.Contains(token)) then failwith ""

                    let role = wsCtx.Headers.["X-Client-Role"]
                    if not (role.Contains("Universal")) then failwith ""

                    let context = new CqWsContext(wsCtx.WebSocket)
                    
                    for m in HandlerModuleBase.AllDefinedModules do 
                        context.RegisterModule(m)
                    
                    context.Start()

                    logger.Info(sprintf "%s 已连接，反向WebSocket" context.SelfId)

                    CqWsContextPool.Instance.AddContext(context)

                with
                | e ->
                    logger.Fatal(sprintf "反向WS连接错误：%O" e)
                    ctx.Response.StatusCode <- 500
                    ctx.Response.Close()

        } |> Async.Start

    interface IDisposable with
        member x.Dispose() = ()