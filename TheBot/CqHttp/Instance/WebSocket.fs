namespace KPX.TheBot.WebSocket.Instance
open System.Collections.Generic
open System
open System.Threading
open System.Net.WebSockets
open KPX.TheBot.WebSocket
open KPX.TheBot.WebSocket.Api
open Newtonsoft.Json
open Newtonsoft.Json.Linq

type ClientEventArgs<'Sender, 'Data>(sender : 'Sender, data : 'Data) = 
    inherit EventArgs()
    member val Sender = sender
    member val Data = data
    member val Response = DataType.Response.EmptyResponse with get, set

///用于管理需要处理Echo的API调用
type ApiCallManager(ws : ClientWebSocket, token : CancellationToken) = 
    let logger = NLog.LogManager.GetCurrentClassLogger()
    let utf8   = Text.Encoding.UTF8
    let getEcho() = (Guid.NewGuid().ToString())
    let pendingApi = new Dictionary<string, ManualResetEvent * ApiRequestBase>()
    let lock = new ReaderWriterLockSlim()

    member x.Call<'T when 'T :> ApiRequestBase>(req : ApiRequestBase)  =
        async {
            let echo = getEcho()
            let mre = new ManualResetEvent(false)
            let json = req.GetRequestJson(echo)

            lock.EnterWriteLock()
            pendingApi.Add(echo, (mre, req)) |> ignore
            lock.ExitWriteLock()

            logger.Trace("请求API：{0}", json)
            let data = json |> utf8.GetBytes
            do! ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, token) |> Async.AwaitTask
            let! ret = Async.AwaitWaitHandle (mre :> WaitHandle)

            lock.EnterWriteLock()
            pendingApi.Remove(echo) |> ignore
            lock.ExitWriteLock()
        }
        |> Async.RunSynchronously
        req :?> 'T

    member x.Process(ret : Api.ApiResponse) =
        logger.Info("收到API调用结果：{0}", sprintf "%A" ret)
        let notEmpty = not <| String.IsNullOrEmpty(ret.Echo)
        lock.EnterReadLock()
        let hasPending = pendingApi.ContainsKey(ret.Echo)
        if notEmpty && hasPending then
            let (mre, api) = pendingApi.[ret.Echo]
            api.HandleResponseData(ret.Data)
            mre.Set() |> ignore
        lock.ExitReadLock()

[<AbstractClass>]
type HandlerModuleBase() as x= 
    let logger = NLog.LogManager.GetLogger(x.GetType().Name)
    let tryToOption (ret, v) = 
        if ret then
            Some(v)
        else
            None
    static member SharedConfig = new Collections.Concurrent.ConcurrentDictionary<string, string>()

    abstract MessageHandler : obj -> ClientEventArgs<CqWebSocketClient, DataType.Event.Message.MessageEvent> -> unit
    abstract RequestHandler : obj -> ClientEventArgs<CqWebSocketClient, DataType.Event.Request.RequestEvent> -> unit
    abstract  NoticeHandler : obj -> ClientEventArgs<CqWebSocketClient,   DataType.Event.Notice.NoticeEvent> -> unit

    default x.MessageHandler _ _ = ()
    default x.RequestHandler _ _ = ()
    default  x.NoticeHandler _ _ = ()

    member x.QuickMessageReply(arg : ClientEventArgs<CqWebSocketClient, DataType.Event.Message.MessageEvent>, msg : string, ?atUser : bool) = 
        let atUser = defaultArg atUser false
        let ctx = arg.Data
        let msg = DataType.Message.Message.TextMessage(msg)
        match ctx with
        | _ when ctx.IsDiscuss -> arg.Response <- DataType.Response.DiscusMessageResponse(msg, atUser)
        | _ when ctx.IsGroup -> arg.Response <- DataType.Response.GroupMessageResponse(msg, atUser, false, false, false, 0)
        | _ when ctx.IsPrivate -> arg.Response <- DataType.Response.PrivateMessageResponse(msg)
        | _ -> 
            logger.Fatal("？")

    ///用于访问共享配置
    member x.Item with get (k:string)   = 
                        tryToOption <| HandlerModuleBase.SharedConfig.TryGetValue(k)
                   and set k v =
                        HandlerModuleBase.SharedConfig.AddOrUpdate(k,v,(fun x y -> v)) |> ignore
                            
and CqWebSocketClient(url, token) =
    let ws  = new ClientWebSocket()
    let cts = new CancellationTokenSource()
    let getEcho() = (Guid.NewGuid().ToString())
    let pendingApi = new Dictionary<string, ManualResetEvent * ApiRequestBase>()
    let utf8 = Text.Encoding.UTF8
    let logger = NLog.LogManager.GetCurrentClassLogger()

    let msgEvent     = new Event<_>()
    let noticeEvent  = new Event<_>()
    let requestEvent = new Event<_>()

    do
        ws.Options.SetRequestHeader("Authorization", sprintf "Bearer %s" token)

    [<CLIEvent>]
    member x.MessageEvent = msgEvent.Publish

    [<CLIEvent>]
    member x.NoticeEvent = noticeEvent.Publish

    [<CLIEvent>]
    member x.RequestEvent = requestEvent.Publish

    member x.RegisterModule(m : #HandlerModuleBase) =
        x.MessageEvent.AddHandler(new Handler<_>(m.MessageHandler))
        x. NoticeEvent.AddHandler(new Handler<_>(m. NoticeHandler))
        x.RequestEvent.AddHandler(new Handler<_>(m.RequestHandler))
        
    member x.IsAvailable  = ws.State = WebSocketState.Open

    member x.Connect() = 
        if not x.IsAvailable then
            logger.Info("正在连接Websocket")
            ws.ConnectAsync(url, cts.Token)
            |> Async.AwaitTask
            |> Async.RunSynchronously

    member x.StartListen() =
        async {
            let buffer = Array.zeroCreate<byte> 4096
            let seg    = new ArraySegment<byte>(buffer)

            let rec readMessage (ms : IO.MemoryStream) =
                let s = ws.ReceiveAsync(seg, cts.Token) |> Async.AwaitTask |> Async.RunSynchronously
                ms.Write(seg.Array, seg.Offset, s.Count)
                if s.EndOfMessage then
                    utf8.GetString(ms.ToArray())
                else
                    readMessage(ms)

            while (true) do
                let json = readMessage(new IO.MemoryStream())
                let obj = JObject.Parse(json)
                if obj.ContainsKey("post_type") then
                    logger.Trace("收到上报：{0}", json)
                    match obj.["post_type"].Value<string>() with
                    | "message" ->
                        let msg = obj.ToObject<KPX.TheBot.WebSocket.DataType.Event.Message.MessageEvent>()
                        logger.Info("收到消息上报：{0}", sprintf "%A" msg)
                        let args = new ClientEventArgs<CqWebSocketClient, DataType.Event.Message.MessageEvent>(x, msg)
                        msgEvent.Trigger(args)
                        x.SendQuickResponse(json, args.Response)
                    | "notice" ->
                        let notice = obj.ToObject<DataType.Event.Notice.NoticeEvent>()
                        let args = new ClientEventArgs<CqWebSocketClient, DataType.Event.Notice.NoticeEvent>(x, notice)
                        noticeEvent.Trigger(args)
                        x.SendQuickResponse(json, args.Response)
                    | "request" -> 
                        let request = obj.ToObject<DataType.Event.Request.RequestEvent>()
                        let args = new ClientEventArgs<CqWebSocketClient, DataType.Event.Request.RequestEvent>(x, request)
                        requestEvent.Trigger(args)
                        x.SendQuickResponse(json, args.Response)
                    | other ->
                        logger.Fatal("未知上报类型：{0}", other)
                elif obj.ContainsKey("retcode") then
                    //API调用结果
                    let ret = obj.ToObject<Api.ApiResponse>()
                    logger.Info("收到API调用结果：{0}", sprintf "%A" ret)
                    let notEmpty = not <| String.IsNullOrEmpty(ret.Echo)
                    let hasPending = pendingApi.ContainsKey(ret.Echo)
                    if notEmpty && hasPending then
                        let (mre, api) = pendingApi.[ret.Echo]
                        api.HandleResponseData(ret.Data)
                        mre.Set() |> ignore

        } |> Async.Start

    member x.StopListen() =
        cts.Cancel()

    member x.CallApi(req : ApiRequestBase) =
        async {
            let echo = getEcho()
            let mre = new ManualResetEvent(false)
            let json = req.GetRequestJson(echo)
            pendingApi.Add(echo, (mre, req)) |> ignore
            logger.Trace("请求API：{0}", json)
            let data = json |> utf8.GetBytes
            do! ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, cts.Token) |> Async.AwaitTask
            let! ret = Async.AwaitWaitHandle (mre :> WaitHandle)
            pendingApi.Remove(echo) |> ignore
        }
        |> Async.RunSynchronously
        

        
    member x.SendQuickResponse(context : string, r : KPX.TheBot.WebSocket.DataType.Response.Response) =
        if r <> DataType.Response.EmptyResponse then
            let rep = new KPX.TheBot.WebSocket.Api.QuickOperation(context)
            rep.Reply <- r

            let json = rep.GetRequestJson()
            logger.Trace("发送回复：{0}", json)
            let data = json |> utf8.GetBytes
            ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, cts.Token)
            |> Async.AwaitTask
            |> Async.Start