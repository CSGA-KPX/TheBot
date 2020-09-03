namespace KPX.FsCqHttp.Instance

open System
open System.Collections.Generic
open System.Threading
open System.Net
open System.Net.WebSockets
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.Handler
open Newtonsoft.Json.Linq


/// Reverse Websocket服务端
type CqWebSocketServer(uriPrefix, token) = 
    let logger = NLog.LogManager.GetCurrentClassLogger()

    let listener = new HttpListener()
    let clients  = Collections.Concurrent.ConcurrentDictionary<uint64, (uint64 * WebSocket)>()

    let ctsMsgLoop = new CancellationTokenSource()
    let ctsListener = new CancellationTokenSource()

    member x.Start() =
        logger.Info("Starting Reverse Websocket server.")
        ()

    member x.Stop() = 
        logger.Info("Stopping Reverse Websocket server.")
        
        //暂停连接

        //关闭所有现有WS


        ()

    member private x.StartListenConnection() = 
        async {
        while not ctsListener.Token.IsCancellationRequested do
            let! ctx = listener.GetContextAsync() |> Async.AwaitTask
            // Async()期间可能会被取消
            if not ctx.Request.IsWebSocketRequest || ctsListener.Token.IsCancellationRequested then
                ctx.Response.StatusCode <- 403
                ctx.Response.Close()
            else
                try
                    let! wsCtx = ctx.AcceptWebSocketAsync("") |> Async.AwaitTask

                    let auth = wsCtx.Headers.["Authorization"]
                    if not (auth.Contains(token)) then failwith ""

                    let role = wsCtx.Headers.["X-Client-Role"]
                    if not (role.Contains("Universal")) then failwith ""

                    let self = wsCtx.Headers.["X-Self-ID"] |> uint64
                    clients.TryAdd(self, (self, wsCtx.WebSocket)) |> ignore

                    logger.Info(sprintf "[UID: %i] 已连接" self)
                    x.StartMessageLoop(self, wsCtx.WebSocket)

                with
                | e ->
                    ctx.Response.StatusCode <- 500
                    ctx.Response.Close()
        } |> Async.Start

    member private x.StartMessageLoop(uid, ws) = 
        async {
            use ms = new IO.MemoryStream()
            let buf = WebSocket.CreateServerBuffer(4096)

            logger.Info(sprintf "[UID: %i] 已启动消息循环" uid)
            try
                while (ws.State = WebSocketState.Open) && (not ctsMsgLoop.Token.IsCancellationRequested) do
                    let recv = ws.ReceiveAsync(buf, ctsMsgLoop.Token) 
                                |> Async.AwaitTask
                                |> Async.RunSynchronously

                    ()
            with
            | e -> ()
        } |> Async.Start

    interface IDisposable with
        member x.Dispose() = ()