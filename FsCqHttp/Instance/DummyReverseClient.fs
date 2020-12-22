namespace KPX.FsCqHttp.Instance

open System
open System.Threading
open System.Net

open Newtonsoft.Json.Linq

open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Api

[<AutoOpen>]
module private Constants =
    [<Literal>]
    let MessageEventJson = """
{
    "font":0,
    "message":[
        {
            "data":{
                "text":"asdasd"
            },
            "type":"text"
        }
    ],
    "message_id":-381156734,
    "message_type":"private",
    "post_type":"message",
    "raw_message":"asdasd",
    "self_id":10000,
    "sender":{
        "age":0,
        "nickname":"Debug",
        "sex":"unknown",
        "user_id":10010
    },
    "sub_type":"friend",
    "time":1600788397,
    "user_id":10010
}
"""

/// 用于FSI调试的虚拟反向Ws客户端
/// 内置Ws服务端
type DummyReverseClient(server, token) as x =
    let cts = new CancellationTokenSource()
    let utf8 = Text.Encoding.UTF8

    let logger =
        NLog.LogManager.GetLogger("DummyReverseClient")

    let defMsgEvent = JObject.Parse(MessageEventJson)
    let wsClient = new WebSockets.ClientWebSocket()

    let apiResponse =
        Collections.Generic.Dictionary<string, obj>()

    do
        x.AddApiResponse(".handle_quick_operation", obj ())

        x.AddApiResponse(
            "get_login_info",
            {| user_id = 10000
               nickname = "Debug" |}
        )

        x.AddApiResponse("can_send_image", {| yes = true |})

    member x.AddApiResponse<'T when 'T :> ApiRequestBase>(obj : obj) =
        let api =
            Activator.CreateInstance(typeof<'T>) :?> ApiRequestBase

        x.AddApiResponse(api.ActionName, obj)

    member x.AddApiResponse(str, obj) = apiResponse.Add(str, obj)

    member x.Start() =
        logger.Info("正在启动")
        x.ServerMessageLoop()

    member x.SendMessage(msg : Message) =
        let rawMsg = msg.ToCqString()
        let me = defMsgEvent.DeepClone() :?> JObject
        me.["message"] <- JArray.FromObject(msg)
        me.["raw_message"] <- JValue(rawMsg)

        let send =
            me.ToString(Newtonsoft.Json.Formatting.None)

        let mem = ArraySegment<byte>(utf8.GetBytes(send))

        logger.Info(sprintf "事件: 发送消息 %s" send)

        wsClient.SendAsync(mem, WebSockets.WebSocketMessageType.Text, true, cts.Token)
        |> Async.AwaitTask
        |> Async.RunSynchronously

    member x.SendMessage(text : string) =
        let msg = Message()
        msg.Add(text)
        x.SendMessage(msg)

    member private x.HandleMessage(json : string) =
        try
            logger.Info(sprintf "事件: 接收到消息 %s" json)
            let obj = JObject.Parse(json)
            let echo = obj.["echo"].Value<string>()
            let action = obj.["action"].Value<string>()

            let param = obj.["params"]
            param |> ignore

            let response = JObject()

            if apiResponse.ContainsKey(action)
            then response.["data"] <- JObject.FromObject(apiResponse.[action])
            else response.["data"] <- JValue(box null)

            response.["echo"] <- JValue(echo)
            response.["retcode"] <- JValue(0)
            response.["status"] <- JValue("ok")

            let ret =
                response.ToString(Newtonsoft.Json.Formatting.None)

            let mem = ArraySegment<byte>(utf8.GetBytes(ret))

            wsClient.SendAsync(mem, WebSockets.WebSocketMessageType.Text, true, cts.Token)
            |> Async.AwaitTask
            |> Async.RunSynchronously

            logger.Info(sprintf "事件: 回复消息 %s" ret)
        with e -> logger.Fatal(sprintf "发生错误：%O" e)

    member private x.ServerMessageLoop() =
        async {
            wsClient.Options.SetRequestHeader("Authorization", sprintf "Bearer %s" token)
            wsClient.Options.SetRequestHeader("X-Client-Role", "Universal")
            logger.Info(sprintf "正在连接%s" server)

            wsClient.ConnectAsync(Uri(server), cts.Token)
            |> Async.AwaitTask
            |> Async.RunSynchronously

            logger.Info("WebSocket已连接")

            let buffer = Array.zeroCreate<byte> 4096
            let seg = ArraySegment<byte>(buffer)
            use ms = new IO.MemoryStream()

            let rec readMessage (ms : IO.MemoryStream) =
                let s =
                    wsClient.ReceiveAsync(seg, cts.Token)
                    |> Async.AwaitTask
                    |> Async.RunSynchronously

                ms.Write(seg.Array, seg.Offset, s.Count)
                if s.EndOfMessage then utf8.GetString(ms.ToArray()) else readMessage (ms)

            try
                while (not cts.IsCancellationRequested) do
                    ms.SetLength(0L)
                    let json = readMessage (ms)

                    Tasks.Task.Run(fun () -> x.HandleMessage(json))
                    |> ignore
            with e ->
                logger.Fatal(e.ToString())
                cts.Cancel()
        }
        |> Async.Start
