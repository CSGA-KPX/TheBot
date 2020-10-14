﻿namespace KPX.FsCqHttp.Instance

open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading
open System.Net.WebSockets

open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.Handler

open Newtonsoft.Json.Linq


type CqWsContext(ws : WebSocket) = 
    static let moduleCache = Dictionary<Type, HandlerModuleBase>()
    let cts = new CancellationTokenSource()
    let utf8 = Text.Encoding.UTF8
    let logger = NLog.LogManager.GetCurrentClassLogger()
    let apiPending = ConcurrentDictionary<string, ManualResetEvent * ApiRequestBase>()
    let started = new ManualResetEvent(false)
    let modules = List<HandlerModuleBase>()
    let self = SystemApi.GetLoginInfo()

    /// 发生链接错误时重启服务器的函数
    ///
    /// 值为None时，不再尝试重连
    member val RestartContext : (unit -> CqWsContext) option = None with get, set

    /// 获取登录号信息
    member x.Self = self

    /// 获取登录号标识符
    member x.SelfId = 
        if self.IsExecuted then
            sprintf "[%i:%s]" self.UserId self.Nickname
        else
            "[--:--]"

    /// 注册模块
    member x.RegisterModule(t : Type) = 
        if moduleCache.ContainsKey(t) then
            moduleCache.[t]
        else
            let isSubClass = t.IsSubclassOf(typeof<HandlerModuleBase>) && (not <| t.IsAbstract)
            if not isSubClass then
                invalidArg "t" "必须是HandlerModuleBase子类"
            let m = Activator.CreateInstance(t) :?> HandlerModuleBase
            if m.IsSharedModule then
                moduleCache.Add(t, m)
            //else
            //    m.ApiCaller <- Some(x :> IApiCallProvider)
            m
        |> modules.Add

    /// 使用API检查是否在线
    member x.CheckOnline() = 
        if ws.State <> WebSocketState.Open then
            false
        else
            try
                let ret = (x :> IApiCallProvider).CallApi<SystemApi.GetStatus>()
                ret.Online && ret.Good
            with
            | _ -> false

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

        (x :> IApiCallProvider).CallApi(self)

    member x.Stop() = cts.Cancel()

    member private x.HandleMessage(json : string) = 
        let obj = JObject.Parse(json)
        let logJson = lazy (obj.ToString(Newtonsoft.Json.Formatting.None))
        let rec getRootExn (exn : exn) = 
            if isNull exn.InnerException then exn
            else getRootExn exn.InnerException

        if obj.ContainsKey("post_type") then //消息上报
            if KPX.FsCqHttp.Config.Logging.LogEventPost then
                logger.Trace(sprintf "%s收到上报：%s" x.SelfId logJson.Value)


            let args = new ClientEventArgs(x, obj)
            try
                for m in modules do m.HandleCqHttpEvent(args)
            with
            | e ->
                let rootExn = getRootExn e
                match rootExn with
                | :? IgnoreException -> ()
                | _ ->
                    args.QuickMessageReply(sprintf "内部错误：%s" rootExn.Message)
                    logger.Error(sprintf "捕获异常：\r\n %O" e)

        elif obj.ContainsKey("retcode") then //API调用结果
            if KPX.FsCqHttp.Config.Logging.LogApiCall then
                logger.Trace(sprintf "%s收到API调用结果： %s" x.SelfId logJson.Value)

            let ret = obj.ToObject<Response.ApiResponse>()
            let hasPending, item = apiPending.TryGetValue(ret.Echo)
            if hasPending then
                let (mre, api) = item
                api.HandleResponse(ret)
                mre.Set() |> ignore
            else
                logger.Warn(sprintf "未注册echo:%s" ret.Echo)

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
                if s.EndOfMessage then utf8.GetString(ms.ToArray())
                else readMessage (ms)

            try
                started.Set() |> ignore
                while (not cts.IsCancellationRequested) do
                    ms.SetLength(0L)
                    let json = readMessage (ms)
                    Tasks.Task.Run((fun () -> x.HandleMessage(json)))
                        .ContinueWith(fun t -> 
                            if t.IsFaulted then
                                logger.Fatal(sprintf "捕获异常：\r\n %O" t.Exception)
                        ) |> ignore
            with
            | e ->
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

                let ret = apiPending.TryAdd(echo, (mre, req))

                if KPX.FsCqHttp.Config.Logging.LogApiCall then 
                    logger.Trace(sprintf "添加echo %s : %b" echo ret)
                    logger.Trace(sprintf "%s请求API：%s" x.SelfId json)

                let data = json |> utf8.GetBytes
                do! ws.SendAsync(ArraySegment<byte>(data), WebSocketMessageType.Text, true, cts.Token) |> Async.AwaitTask
                let! _ = Async.AwaitWaitHandle(mre :> WaitHandle)

                apiPending.TryRemove(echo) |> ignore

                if KPX.FsCqHttp.Config.Logging.LogApiCall then 
                    logger.Trace(sprintf "echo:%s 已完成" echo)

                req.IsExecuted <- true
            }
            |> Async.RunSynchronously

        member x.CallApi<'T when 'T :> ApiRequestBase and 'T : (new : unit -> 'T)>() =
            let req = Activator.CreateInstance<'T>()
            (x :> IApiCallProvider).CallApi(req)
            req

    interface IDisposable with
        member x.Dispose() = x.Stop()

and CqWsContextPool private () = 
    static let instance = CqWsContextPool()
    let logger = NLog.LogManager.GetCurrentClassLogger()

    let pool = ConcurrentDictionary<uint64, CqWsContext>()

    member x.AddContext(context : CqWsContext) =
        pool.TryAdd(context.Self.UserId, context) |> ignore
        logger.Info(sprintf "已接受连接:%s" context.SelfId)

    member x.RemoveContext(context : CqWsContext) =
        pool.TryRemove(context.Self.UserId) |> ignore
        logger.Info(sprintf "已移除连接:%s" context.SelfId)

    interface Collections.IEnumerable with
        member x.GetEnumerator() = pool.Values.GetEnumerator() :> Collections.IEnumerator

    interface Collections.Generic.IEnumerable<CqWsContext> with
        member x.GetEnumerator() = pool.Values.GetEnumerator()

    static member Instance : CqWsContextPool = instance