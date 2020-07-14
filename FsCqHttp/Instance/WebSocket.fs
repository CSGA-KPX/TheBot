namespace KPX.FsCqHttp.Instance

open System
open System.Collections.Generic
open System.Threading
open System.Net.WebSockets
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.Handler
open Newtonsoft.Json.Linq

type CqWebSocketClient(url, token) =
    static let moduleCache = Dictionary<Type, HandlerModuleBase>()

    let ws = new ClientWebSocket()
    let cts = new CancellationTokenSource()
    let utf8 = Text.Encoding.UTF8
    let logger = NLog.LogManager.GetCurrentClassLogger()

    let apiPending = Dictionary<string, ManualResetEvent * ApiRequestBase>()
    let apiLock = new ReaderWriterLockSlim()

    let cqHttpEvent = Event<_>()

    do
        ws.Options.SetRequestHeader("Authorization", sprintf "Bearer %s" token)

    [<CLIEvent>]
    member x.OnCqHttpEvent = cqHttpEvent.Publish

    member x.RegisterModule(t : Type) = 
        let m = 
            if moduleCache.ContainsKey(t) then
                moduleCache.[t]
            else
                let isSubClass = t.IsSubclassOf(typeof<HandlerModuleBase>) && (not <| t.IsAbstract)
                if not isSubClass then
                    invalidArg "t" "必须是HandlerModuleBase子类"
                let m = Activator.CreateInstance(t) :?> HandlerModuleBase
                if m.IsSharedModule then
                    moduleCache.Add(t, m)
                else
                    m.ApiCaller <- Some(x :> IApiCallProvider)
                m
        x.OnCqHttpEvent.AddHandler(Handler<_>(m.HandleCqHttpEvent))

    member x.IsAvailable = ws.State = WebSocketState.Open

    interface IApiCallProvider with
        member x.CallApi(req) =
            async {
                let echo = Guid.NewGuid().ToString()
                let mre = new ManualResetEvent(false)
                let json = req.GetRequestJson(echo)

                apiLock.EnterWriteLock()
                apiPending.Add(echo, (mre, req)) |> ignore
                apiLock.ExitWriteLock()

                if KPX.FsCqHttp.Config.Logging.LogApiCall then 
                    logger.Trace("请求API：{0}", json)
                let data = json |> utf8.GetBytes
                do! ws.SendAsync(ArraySegment<byte>(data), WebSocketMessageType.Text, true, cts.Token) |> Async.AwaitTask
                let! ret = Async.AwaitWaitHandle(mre :> WaitHandle)

                apiLock.EnterWriteLock()
                apiPending.Remove(echo) |> ignore
                apiLock.ExitWriteLock()
            }
            |> Async.RunSynchronously

        member x.CallApi<'T when 'T :> ApiRequestBase and 'T : (new : unit -> 'T)>() =
            let req = Activator.CreateInstance<'T>()
            (x :> IApiCallProvider).CallApi(req)
            req

    member x.Connect() =
        if not x.IsAvailable then
            logger.Info("正在连接Websocket")
            ws.ConnectAsync(url, cts.Token)
            |> Async.AwaitTask
            |> Async.RunSynchronously
            logger.Info("已连接Websocket")


    member private x.HandleMessage(json : string) =
        let obj = JObject.Parse(json)
        // 用单行覆盖掉
        let json = obj.ToString(Newtonsoft.Json.Formatting.None)
        if obj.ContainsKey("post_type") then
            if KPX.FsCqHttp.Config.Logging.LogEventPost then
                logger.Trace("收到上报：{0}", json)
            let args = new ClientEventArgs(x, obj)
            cqHttpEvent.Trigger(args)
        elif obj.ContainsKey("retcode") then
            //API调用结果
            if KPX.FsCqHttp.Config.Logging.LogApiCall then
                logger.Trace("收到API调用结果：{0}", sprintf "%A" json)
            let ret = obj.ToObject<Response.ApiResponse>()
            apiLock.EnterReadLock()
            let notEmpty = not <| String.IsNullOrEmpty(ret.Echo)
            let hasPending = apiPending.ContainsKey(ret.Echo)
            if notEmpty && hasPending then
                let (mre, api) = apiPending.[ret.Echo]
                api.HandleResponse(ret)
                mre.Set() |> ignore
            apiLock.ExitReadLock()


    member x.StartListen() =
        async {
            let buffer = Array.zeroCreate<byte> 4096
            let seg = ArraySegment<byte>(buffer)

            let rec readMessage (ms : IO.MemoryStream) =
                let s =
                    ws.ReceiveAsync(seg, cts.Token)
                    |> Async.AwaitTask
                    |> Async.RunSynchronously
                ms.Write(seg.Array, seg.Offset, s.Count)
                if s.EndOfMessage then utf8.GetString(ms.ToArray())
                else readMessage (ms)

            while (true) do
                let json = readMessage (new IO.MemoryStream())
                Tasks.Task.Run((fun () -> x.HandleMessage(json))) |> ignore
        }
        |> Async.Start

    member x.StopListen() = cts.Cancel()
