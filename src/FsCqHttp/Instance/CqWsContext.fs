namespace rec KPX.FsCqHttp.Instance

open System
open System.IO
open System.Collections.Concurrent
open System.Threading
open System.Net.WebSockets

open KPX.FsCqHttp
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.Api.System
open KPX.FsCqHttp.Handler

open Newtonsoft.Json.Linq

[<AbstractClass>]
type CqWsContextBase() =
    /// 发生链接错误时重启服务器的函数
    ///
    /// 值为None时，不再尝试重连
    abstract RestartContext : (unit -> CqWsContext) option with get, set

    /// 使用API检查是否在线
    abstract IsOnline : bool

    /// 获取Context内已经定义的模块信息
    member val Modules = ContextModuleInfo()

    /// 返回已经定义的指令，结果无序
    member x.Commands =
        x.Modules.Commands.Values |> Seq.cast<CommandInfo>

    /// 获取登录号昵称
    abstract BotNickname : string

    /// 获取登录号信息
    abstract BotUserId : UserId

    /// 获取登录号标识符
    abstract BotIdString : string

    /// 启用消息循环
    abstract Start : unit -> unit

    abstract Stop : unit -> unit

    abstract CallApi<'T when 'T :> ApiBase> : 'T -> 'T

    interface IApiCallProvider with
        member x.ProviderUserId = x.BotUserId
        member x.ProviderId = x.BotIdString
        member x.ProviderName = x.BotNickname

        member x.CallApi<'T when 'T :> ApiBase>(req : 'T) = x.CallApi(req)

        /// 调用一个不需要额外设定的api
        member x.CallApi<'T when 'T :> ApiBase and 'T : (new : unit -> 'T)>() =
            let req = Activator.CreateInstance<'T>()
            (x :> IApiCallProvider).CallApi(req)

[<AbstractClass>]
type WsContextApiBase() =
    inherit ApiBase()

    abstract Invoke : CqWsContextBase -> unit

type CqWsContextPool private () =
    let logger = NLog.LogManager.GetCurrentClassLogger()

    let pool =
        ConcurrentDictionary<UserId, CqWsContextBase>()

    member x.AddContext(context : CqWsContextBase) =
        pool.TryAdd(context.BotUserId, context) |> ignore
        logger.Info $"已接受连接:%s{context.BotIdString}"

        CqWsContextPool.ContextModuleLoader.RegisterModuleFor(context.BotUserId, context.Modules)

    member x.RemoveContext(context : CqWsContextBase) =
        pool.TryRemove(context.BotUserId) |> ignore
        logger.Info $"已移除连接:%s{context.BotIdString}"

    interface Collections.IEnumerable with
        member x.GetEnumerator() =
            pool.Values.GetEnumerator() :> Collections.IEnumerator

    interface Collections.Generic.IEnumerable<CqWsContextBase> with
        member x.GetEnumerator() = pool.Values.GetEnumerator()

    static member val Instance = CqWsContextPool()

    static member val internal ContextModuleLoader : ContextModuleLoader =
        ContextModuleLoader(Array.empty) with get, set


type CqWsContext(ws : WebSocket) =
    inherit CqWsContextBase()

    let cts = new CancellationTokenSource()
    let utf8 = Text.Encoding.UTF8
    let logger = NLog.LogManager.GetCurrentClassLogger()

    let apiPending =
        ConcurrentDictionary<string, ManualResetEvent * CqHttpApiBase>()

    let started = new ManualResetEvent(false)

    let self = GetLoginInfo()

    override val RestartContext : (unit -> CqWsContext) option = None with get, set

    override x.BotNickname = self.Nickname

    override x.BotUserId = self.UserId

    override x.BotIdString =
        if self.IsExecuted then
            $"[%i{self.UserId.Value}:%s{self.Nickname}]"
        else
            "[--:--]"

    override x.Start() =
        if cts.IsCancellationRequested then
            logger.Fatal("无法启动消息循环：已调用Stop或WS异常终止")
            invalidOp "CancellationRequested"

        if ws.State <> WebSocketState.Open then
            logger.Fatal("无法启动消息循环：WebSocketState状态检查失败。")
            logger.Fatal $"%A{ws.State} %A{ws.CloseStatus} %A{ws.CloseStatusDescription}"
            cts.Cancel()
            invalidOp "WebSocketState <> Open"

        x.StartMessageLoop()
        started.WaitOne() |> ignore

        (x :> IApiCallProvider).CallApi(self) |> ignore

    override x.Stop() =
        cts.Cancel()
        ws.Abort()

    override x.IsOnline =
        if ws.State <> WebSocketState.Open then
            false
        else
            try
                let ret =
                    (x :> IApiCallProvider).CallApi<GetLoginInfo>()

                not <| isNull ret.Nickname
            with
            | _ -> false

    override x.CallApi(req) =
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

                if Config.LogApiCall then
                    logger.Trace $"%s{x.BotIdString}请求API：%s{httpApi.ActionName}"

                    if Config.LogApiJson then
                        logger.Trace $"%s{x.BotIdString}请求API：%s{json}"

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

    member private x.HandleApiResponse(ret : ApiResponse) =
        let hasPending, item = apiPending.TryGetValue(ret.Echo)

        if hasPending then
            let mre, api = item
            api.HandleResponse(ret)
            mre.Set() |> ignore
        else
            logger.Warn $"未注册echo:%s{ret.Echo}"

    member private x.HandleMessage(json : string) =
        try
            let ctx = PostContent(JObject.Parse(json))

            if (ctx.RawEventPost.ContainsKey("post_type")) then //消息上报
                if Config.LogEventPost then
                    logger.Trace $"%s{x.BotIdString}收到上报：{ctx}"

                match CqEventArgs.Parse(x, ctx) with
                | :? CqMetaEventArgs when x.Modules.MetaCallbacks.Count = 0 -> ()
                | :? CqNoticeEventArgs when x.Modules.NoticeCallbacks.Count = 0 -> ()
                | :? CqRequestEventArgs when x.Modules.RequestCallbacks.Count = 0 -> ()
                | :? CqMessageEventArgs as args when
                    (x.Modules.TryCommand(args).IsNone)
                    && x.Modules.MessageCallbacks.Count = 0
                    ->
                    ()
                | :? CqMessageEventArgs as args ->
                    let isCmd = x.Modules.TryCommand(args)

                    if isCmd.IsSome then
                        if isCmd.Value.CommandAttribute.ExecuteImmediately then
                            // 需要立刻执行的指令，不通过调度器
                            let ci = isCmd.Value

                            let cmdArgs =
                                CommandEventArgs(args, ci.CommandAttribute)

                            match ci.MethodAction with
                            | MethodAction.ManualAction action -> action.Invoke(cmdArgs)
                            | MethodAction.AutoAction func -> func.Invoke(cmdArgs).Response(cmdArgs)
                        else
                            // 正常指令走调度器
                            TaskScheduler.enqueue (x.Modules, args)
                    else if x.Modules.MessageCallbacks.Count <> 0 then
                        // 如果有其他类需要监听消息事件
                        TaskScheduler.enqueue (x.Modules, args)
                | args -> TaskScheduler.enqueue (x.Modules, args)

            elif ctx.RawEventPost.ContainsKey("retcode") then //API调用结果
                if Config.LogApiCall then
                    logger.Trace $"%s{x.BotIdString}收到API调用结果： {ctx}"

                x.HandleApiResponse(ctx.RawEventPost.ToObject<ApiResponse>())
        with
        | e -> logger.Warn $"%s{x.BotIdString}WS处理消息异常：\r\n%A{e}"

    member private x.StartMessageLoop() =
        let rec readMessage
            (ms : MemoryStream)
            (seg : ArraySegment<_>)
            (cts : CancellationTokenSource)
            =
            let s =
                ws.ReceiveAsync(seg, cts.Token)
                |> Async.AwaitTask
                |> Async.RunSynchronously

            ms.Write(seg.Array, seg.Offset, s.Count)

            if s.EndOfMessage then
                utf8.GetString(ms.ToArray())
            else
                readMessage ms seg cts

        async {
            // 长时间执行，所以使用新线程
            do! Async.SwitchToNewThread()

            let seg =
                ArraySegment<byte>(Array.zeroCreate 4096)

            use ms = new MemoryStream()

            try
                started.Set() |> ignore

                while (not cts.IsCancellationRequested) do
                    ms.SetLength(0L)
                    let json = readMessage ms seg cts
                    x.HandleMessage(json)
            with
            | e ->
                logger.Fatal $"%s{x.BotIdString}WS读取捕获异常：\r\n%A{e}"
                CqWsContextPool.Instance.RemoveContext(x)
                x.Stop()

                if x.RestartContext.IsSome then
                    logger.Warn $"%s{x.BotIdString}正在尝试重新连接"
                    CqWsContextPool.Instance.RemoveContext(x.RestartContext.Value())
        }
        |> Async.Start

    interface IDisposable with
        member x.Dispose() = x.Stop()
