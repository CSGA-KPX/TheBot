namespace KPX.FsCqHttp.Instance
open System
open System.Threading
open System.Net.WebSockets
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.Instance.Base
open KPX.FsCqHttp.DataType.Event
open Newtonsoft.Json.Linq

type CqWebSocketClient(url, token) =
    let ws  = new ClientWebSocket()
    let cts = new CancellationTokenSource()
    let man = new ApiCallManager(ws, cts.Token)
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

    member private x.HandleMessage(json : string) = 
        let obj = JObject.Parse(json)
        if obj.ContainsKey("post_type") then
            logger.Trace("收到上报：{0}", json)
            let event = EventUnion.From(obj)
            let args  = new ClientEventArgs(man, json, event)
            match obj.["post_type"].Value<string>() with
            | "message" ->
                logger.Info("收到消息上报：{0}", event.AsMessageEvent)
                msgEvent.Trigger(args)
            | "notice" ->
                noticeEvent.Trigger(args)
            | "request" -> 
                requestEvent.Trigger(args)
            | other ->
                logger.Fatal("未知上报类型：{0}", other)
        elif obj.ContainsKey("retcode") then
            //API调用结果
            logger.Trace("收到API调用结果：{0}", sprintf "%A" json)
            let ret = obj.ToObject<Response.ApiResponse>()
            man.HandleResponse(ret)
    
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
                Tasks.Task.Run((fun () -> x.HandleMessage(json))) |> ignore

        } |> Async.Start

    member x.StopListen() =
        cts.Cancel()

    (*
    member x.CallApi<'T when 'T :> ApiBase.ApiRequestBase>(req : ApiBase.ApiRequestBase) =
        logger.Info("Calling API")
        man.Call<'T>(req)

    member x.SendQuickResponse(context : string, r : KPX.FsCqHttp.DataType.Response.MessageResponse) =
        if r <> Response.EmptyResponse then
            let rep = new SystemApi.QuickOperation(context)
            rep.Reply <- r
            x.CallApi<SystemApi.QuickOperation>(rep) |> ignore
    *)