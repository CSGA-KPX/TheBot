namespace KPX.TheBot.WebSocket
open System.Collections.Generic
open System
open System.Threading
open System.Net.WebSockets
open KPX.TheBot.WebSocket.Api
open Newtonsoft.Json
open Newtonsoft.Json.Linq


type ClientEventArgs<'Sender, 'Data>(sender : 'Sender, data : 'Data) = 
    inherit EventArgs()
    member val Sender = sender
    member val Data = data
    member val Response = DataType.Response.EmptyResponse with get, set

type  CqWebSocketClient(url, token) =
    let ws  = new ClientWebSocket()
    let cts = new CancellationTokenSource()
    let getEcho() = (Guid.NewGuid().ToString())
    let pendingApi = new Dictionary<string, ApiRequestBase>()
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

    member x.IsAvailable  = ws.State = WebSocketState.Open

    member x.StartListen() =
        if not x.IsAvailable then
            logger.Info("正在连接Websocket")
            ws.ConnectAsync(url, cts.Token)
            |> Async.AwaitTask
            |> Async.RunSynchronously
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
                    let ret = obj.ToObject<KPX.TheBot.WebSocket.Api.ApiResponse>()
                    logger.Info("收到API调用结果：{0}", sprintf "%A" ret)
                    ()

        } |> Async.Start

    member x.StopListen() =
        cts.Cancel()

    member x.CallApi() =
        raise <| NotImplementedException()

    member x.SendQuickResponse(context : string, r : KPX.TheBot.WebSocket.DataType.Response.Response) =
        let rep = new KPX.TheBot.WebSocket.Api.QuickOperation(context)
        rep.Reply <- r

        let json = rep.RequestJson
        logger.Trace("发送回复：{0}", json)
        let data = json |> utf8.GetBytes
        ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, cts.Token)
        |> Async.AwaitTask
        |> Async.Start