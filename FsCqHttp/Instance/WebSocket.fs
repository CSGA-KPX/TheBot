namespace KPX.FsCqHttp.Instance
open System
open System.Threading
open System.Net.WebSockets
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.Handler.Base
open KPX.FsCqHttp.DataType.Event
open Newtonsoft.Json.Linq

type CqWebSocketClient(url, token) =
    let ws  = new ClientWebSocket()
    let cts = new CancellationTokenSource()
    let man = new ApiCallManager(ws, cts.Token)
    let utf8 = Text.Encoding.UTF8
    let logger = NLog.LogManager.GetCurrentClassLogger()

    let  cqHttpEvent = new Event<_>()

    do
        ws.Options.SetRequestHeader("Authorization", sprintf "Bearer %s" token)

    [<CLIEvent>]
    member x.OnCqHttpEvent = cqHttpEvent.Publish

    member x.RegisterModule(m : #HandlerModuleBase) =
        x. OnCqHttpEvent.AddHandler(new Handler<_>(m.HandleCqHttpEvent))
        
    member x.IsAvailable  = ws.State = WebSocketState.Open

    member x.Connect() = 
        if not x.IsAvailable then
            logger.Info("正在连接Websocket")
            ws.ConnectAsync(url, cts.Token)
            |> Async.AwaitTask
            |> Async.RunSynchronously
            logger.Info("已连接Websocket")

    member private x.HandleMessage(json : string) = 
        let obj = JObject.Parse(json)
        if obj.ContainsKey("post_type") then
            logger.Trace("收到上报：{0}", json)
            let event = EventUnion.From(obj)
            let args  = new ClientEventArgs(man, json, event)
            cqHttpEvent.Trigger(args)
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