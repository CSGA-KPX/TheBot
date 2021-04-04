namespace rec KPX.FsCqHttp.Instance

open System
open System.Collections.Concurrent
open System.Threading
open System.Net.WebSockets

open KPX.FsCqHttp.Api
open KPX.FsCqHttp.Api.System
open KPX.FsCqHttp.Handler

open Newtonsoft.Json.Linq


[<AbstractClass>]
type WsContextApiBase() =
    inherit ApiBase()

    abstract Invoke : CqWsContext -> unit

type CqWsContextPool private () =
    let logger = NLog.LogManager.GetCurrentClassLogger()

    let pool =
        ConcurrentDictionary<uint64, CqWsContext>()

    member x.AddContext(context : CqWsContext) =
        pool.TryAdd(context.BotUserId, context) |> ignore

        CqWsContextPool.ModuleLoader.RegisterModuleFor(context.BotUserId, context.ModulesInfo)

        logger.Info(sprintf "已接受连接:%s" context.BotIdString)

    member x.RemoveContext(context : CqWsContext) =
        pool.TryRemove(context.BotUserId) |> ignore
        logger.Info(sprintf "已移除连接:%s" context.BotIdString)

    interface Collections.IEnumerable with
        member x.GetEnumerator() =
            pool.Values.GetEnumerator() :> Collections.IEnumerator

    interface Collections.Generic.IEnumerable<CqWsContext> with
        member x.GetEnumerator() = pool.Values.GetEnumerator()

    static member val Instance = CqWsContextPool()

    /// 获取或设置为Context添加模块的加载器
    static member val ModuleLoader : ContextModuleLoader =
        DefaultContextModuleLoader() :> ContextModuleLoader with get, set

type CqWsContext(ws : WebSocket) =
    let cts = new CancellationTokenSource()
    let utf8 = Text.Encoding.UTF8
    let logger = NLog.LogManager.GetCurrentClassLogger()

    let apiPending =
        ConcurrentDictionary<string, ManualResetEvent * CqHttpApiBase>()

    let started = new ManualResetEvent(false)

    let self = GetLoginInfo()

    let rec getRootExn (exn : exn) =
        if isNull exn.InnerException then
            exn
        else
            getRootExn exn.InnerException

    /// 返回已经定义的指令，结果无序。
    member x.Commands =
        x.ModulesInfo.Commands.Values
        |> Seq.cast<CommandInfo>

    /// 发生链接错误时重启服务器的函数
    ///
    /// 值为None时，不再尝试重连
    member val RestartContext : (unit -> CqWsContext) option = None with get, set

    /// 获取登录号昵称
    member x.BotNickname = self.Nickname

    /// 获取登录号信息
    member x.BotUserId = self.UserId

    /// 获取登录号标识符
    member x.BotIdString =
        if self.IsExecuted then
            sprintf "[%i:%s]" self.UserId self.Nickname
        else
            "[--:--]"

    member val ModulesInfo = ContextModuleInfo()

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

    member private x.HandleApiResponse(ret : ApiResponse) =
        let hasPending, item = apiPending.TryGetValue(ret.Echo)

        if hasPending then
            let (mre, api) = item
            api.HandleResponse(ret)
            mre.Set() |> ignore
        else
            logger.Warn(sprintf "未注册echo:%s" ret.Echo)

    member private x.HandleMessage(json : string) =
        let ctx = EventContext(JObject.Parse(json))

        if (ctx.Event.ContainsKey("post_type")) then //消息上报
            if KPX.FsCqHttp.Config.Logging.LogEventPost then
                logger.Trace(sprintf "%s收到上报：%O" x.BotIdString ctx)

            match CqEventArgs.Parse(x, ctx) with
            | :? CqMetaEventArgs when x.ModulesInfo.MetaCallbacks.Count = 0 -> ()
            | :? CqNoticeEventArgs when x.ModulesInfo.NoticeCallbacks.Count = 0 -> ()
            | :? CqRequestEventArgs when x.ModulesInfo.RequestCallbacks.Count = 0 -> ()
            | :? CqMessageEventArgs as args when
                (x.ModulesInfo.TryCommand(args).IsNone)
                && x.ModulesInfo.MessageCallbacks.Count = 0 -> ()
            | args ->
                Tasks.Task.Run(fun () -> TaskScheduler.Enqueue(x.ModulesInfo, args))
                |> ignore

        elif ctx.Event.ContainsKey("retcode") then //API调用结果
            if KPX.FsCqHttp.Config.Logging.LogApiCall then
                logger.Trace(sprintf "%s收到API调用结果： %O" x.BotIdString ctx)

            x.HandleApiResponse(ctx.Event.ToObject<ApiResponse>())

    member private x.StartMessageLoop() =
        async {
            do! Async.SwitchToNewThread()

            let buffer = Array.zeroCreate<byte> 4096
            let seg = ArraySegment<byte>(buffer)
            use ms = new IO.MemoryStream()

            let rec readMessage (ms : IO.MemoryStream) =
                let s =
                    ws.ReceiveAsync(seg, cts.Token)
                    |> Async.AwaitTask
                    |> Async.RunSynchronously

                ms.Write(seg.Array, seg.Offset, s.Count)

                if s.EndOfMessage then
                    utf8.GetString(ms.ToArray())
                else
                    readMessage (ms)

            try
                started.Set() |> ignore

                while (not cts.IsCancellationRequested) do
                    ms.SetLength(0L)
                    let json = readMessage (ms)
                    x.HandleMessage(json)
            with e ->
                cts.Cancel()
                logger.Fatal(sprintf "%sWS读取捕获异常：%A" x.BotIdString e)
                CqWsContextPool.Instance.RemoveContext(x)

                if x.RestartContext.IsSome then
                    logger.Warn(sprintf "%s正在尝试重新连接" x.BotIdString)
                    CqWsContextPool.Instance.RemoveContext(x.RestartContext.Value())
        }
        |> Async.Start

    interface IApiCallProvider with
        
        member x.CallerUserId = x.BotUserId

        member x.CallerId = x.BotIdString
        
        member x.CallerName = x.BotNickname

        member x.CallApi(req) =
            started.WaitOne() |> ignore

            match req :> ApiBase with
            | :? WsContextApiBase as ctxApi ->
                ctxApi.Invoke(x)
                ctxApi.IsExecuted <- true
            | :? CqHttpApiBase as httpApi ->
                async {
                    let echo = Guid.NewGuid().ToString()
                    let mre = new ManualResetEvent(false)
                    let json = httpApi.GetRequestJson(echo)

                    apiPending.TryAdd(echo, (mre, httpApi)) |> ignore

                    if KPX.FsCqHttp.Config.Logging.LogApiCall then
                        logger.Trace(sprintf "%s请求API：%s" x.BotIdString httpApi.ActionName)

                        if KPX.FsCqHttp.Config.Logging.LogApiJson then
                            logger.Trace(sprintf "%s请求API：%s" x.BotIdString json)

                    let data = json |> utf8.GetBytes

                    do!
                        ws.SendAsync(
                            ArraySegment<byte>(data),
                            WebSocketMessageType.Text,
                            true,
                            cts.Token
                        )
                        |> Async.AwaitTask

                    let! _ = Async.AwaitWaitHandle(mre :> WaitHandle)

                    apiPending.TryRemove(echo) |> ignore

                    httpApi.IsExecuted <- true
                }
                |> Async.RunSynchronously
            | _ -> invalidArg (nameof req) "未知API类型"

            req

        member x.CallApi<'T when 'T :> ApiBase and 'T : (new : unit -> 'T)>() =
            let req = Activator.CreateInstance<'T>()
            (x :> IApiCallProvider).CallApi(req)

    interface IDisposable with
        member x.Dispose() = x.Stop()
