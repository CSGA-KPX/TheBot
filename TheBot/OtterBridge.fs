module TheBot.Module.OtterBridge

open System
open System.Reflection
open System.Threading
open KPX.FsCqHttp.Api
open System.Net.WebSockets
open KPX.FsCqHttp.Handler.CommandHandlerBase
open Newtonsoft.Json.Linq
open TheBot.Utils.Config

type MessageEvent = KPX.FsCqHttp.DataType.Event.Message.MessageEvent

let private removeRes() = 
    let a = typeof<System.Net.WebHeaderCollection>.Assembly
    let t = 
        a.GetType("System.Net.HeaderInfoTable").GetFields(BindingFlags.NonPublic ||| BindingFlags.Static)
        |> Array.find (fun x -> x.Name = "HeaderHashTable")
    let f =
        a.GetType("System.Net.HeaderInfo").GetFields(BindingFlags.NonPublic ||| BindingFlags.Instance)
        |> Array.find (fun x -> x.Name = "IsRequestRestricted")
    let ret = t.GetValue(null) :?> Collections.Hashtable
    for entry in ret do 
        let entry = entry :?> System.Collections.DictionaryEntry
        let key = entry.Key :?> string
        let hi = entry.Value
        let value = f.GetValue(hi) :?> bool
        if value then
            f.SetValue(hi, false)

type OtterBridge() as x = 
    inherit CommandHandlerBase(false)

    let url = "wss://xn--v9x.net/ws/event/"
    let mutable ws = new ClientWebSocket()
    let mutable cts = new CancellationTokenSource()
    let wsLock = obj()
    let mutable wsError : Exception option = None

    let utf8 = Text.Encoding.UTF8
    let cm = ConfigManager(ConfigOwner.System)

    let getKey(e : MessageEvent) = 
        if   e.IsPrivate then sprintf "Enable-Tata-Private-%i" e.UserId
        elif e.IsDiscuss then sprintf "Enable-Tata-Disscuss-%i" e.DiscussId
        elif e.IsGroup   then sprintf "Enable-Tata-Group-%i" e.GroupId
        else
            failwithf "未知聊天类型！%O" e

    let waitTask (t : Tasks.Task) = 
        t.ConfigureAwait(false)
            .GetAwaiter()
            .GetResult()

    do
        removeRes()
        x.StartWebSocket()
          
    member private x.StartWebSocket() = 
        // 没出过错才连接
        if wsError.IsNone then
            //关闭已有连接
            if ws.State = WebSocketState.Open then
                ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", cts.Token) |> waitTask
            if ws.State <> WebSocketState.None then
                cts.Cancel()
                ws <- new ClientWebSocket()
                cts <- new CancellationTokenSource()
            ws.Options.SetRequestHeader("User-Agent", "CQHttp/4.13.0")
            ws.Options.SetRequestHeader("Authorization", "Token Authorization")
            ws.Options.SetRequestHeader("X-Self-ID", "3084801066")
            ws.Options.SetRequestHeader("X-Client-Role", "Universal")
            try
                ws.ConnectAsync(Uri(url), cts.Token) |> waitTask
            with
            | e -> wsError <- Some(e)
            x.StartMessageLoop()

    member private x.StartMessageLoop () = 
        x.Logger.Info("正在启动消息处理")
        Tasks.Task.Run(fun () -> 
            let buffer = Array.zeroCreate<byte> 4096
            let seg = ArraySegment<byte>(buffer)

            let rec readMessage (ms : IO.MemoryStream) =
                let s =
                    ws.ReceiveAsync(seg, cts.Token)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult()
                ms.Write(seg.Array, seg.Offset, s.Count)
                if s.EndOfMessage then utf8.GetString(ms.ToArray())
                else readMessage (ms)

            while (ws.State = WebSocketState.Open) do
                let json = readMessage (new IO.MemoryStream())
                let obj  = JObject.Parse(json)
                x.Logger.Info(sprintf "收到下游WS上报:%s" json)
                let action = obj.["action"].Value<string>()
                if action = "get_group_member_list" then
                    x.Logger.Info("已无视群成员列表请求")
                else
                    let api = {new ApiRequestBase(action) with 
                                    member x.WriteParams(w, js) =
                                        let js = obj.["params"].ToString().Trim('{', '}')
                                        w.WriteRaw(js) }
                    x.ApiCaller.Value.CallApi(api)
            x.Logger.Fatal(sprintf "消息循环已停止 %O %O %s %O" ws.State ws.CloseStatus ws.CloseStatusDescription wsError)
        ) |> ignore

    [<CommandHandlerMethodAttribute("#tatastatus", "查看獭獭桥接状态", "")>]
    member x.HandleTataStatus(msgArg : CommandArgs) = 
        use sw = new IO.StringWriter()

        let now = cm.Get<bool>(getKey(msgArg.MessageEvent), false)
        sw.WriteLine("当前命令可用：{0}", now)
        sw.WriteLine("CancellationTokenSource已触发：{0}", cts.IsCancellationRequested)
        sw.WriteLine("Websocket状态：{0}", ws.State)
        sw.WriteLine("CloseStatus状态：{0} : {1}", ws.CloseStatus, ws.CloseStatusDescription)
        sw.WriteLine("wsError状态：{0}", wsError)

        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("#tata", "(群管理) 允许/拒绝桥接獭獭bot", "")>]
    member x.HandleTataCmd(msgArg : CommandArgs) = 
        let e = msgArg.MessageEvent
        if e.IsGroup && e.Sender.Role = "member" then 
            failwith "你不是管理员"
        if e.IsDiscuss then failwith "暂不支持讨论组"
        if e.IsPrivate then failwith "私聊一直都能用哦"
        let key = getKey(msgArg.MessageEvent)
        let now = cm.Get<bool>(key, false) |> not
        cm.Put(key, now)
        if now then
            msgArg.CqEventArgs.QuickMessageReply("已启用獭獭桥接，要禁用就再来一次")
        else
            msgArg.CqEventArgs.QuickMessageReply("已禁用獭獭桥接，要启用就再来一次")

    override x.HandleMessage(arg, e) =
        base.HandleMessage(arg, e)

        if e.Message.ToString().StartsWith("/") then
            let allow = e.IsPrivate || cm.Get<bool>(getKey(e), false)
            if allow then 
                let obj = arg.RawEvent.DeepClone() :?> JObject
                obj.["message"] <- Newtonsoft.Json.Linq.JToken.FromObject(e.Message.ToCqString())
                obj.Property("raw_message").Remove()
                let json = obj.ToString()
                lock wsLock (fun () ->
                    if ws.State <> WebSocketState.Open then
                        x.Logger.Info("正在连接獭獭")
                        x.StartWebSocket()
                    x.Logger.Info(sprintf "调用獭獭：%s" json)
                    let task = ws.SendAsync(ArraySegment<byte>(utf8.GetBytes(json)), WebSocketMessageType.Text, true, cts.Token)
                    task.ConfigureAwait(false)
                          .GetAwaiter()
                          .GetResult()
                    if task.IsFaulted then
                        x.Logger.Fatal(sprintf "獭獭调用发生异常：%O" task.Exception))

    interface IDisposable with
        member x.Dispose() = 
            ws.Dispose()
            cts.Dispose()