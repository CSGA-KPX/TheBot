module TheBot.Module.OtterBridge

open System
open System.Reflection
open System.Collections.Generic
open System.Threading
open KPX.FsCqHttp.Api
open System.Net.WebSockets
open KPX.FsCqHttp.Handler.CommandHandlerBase
open Newtonsoft.Json.Linq
open TheBot.Utils.Config
open KPX.FsCqHttp.Handler.Base

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

    let ws = new ClientWebSocket()
    let cts = new CancellationTokenSource()
    let utf8 = Text.Encoding.UTF8

    let url = "wss://xn--v9x.net/ws/event/"
    let cm = ConfigManager(ConfigOwner.System)

    let getKey(e : MessageEvent) = 
        if   e.IsPrivate then sprintf "Enable-Tata-Private-%i" e.UserId
        elif e.IsDiscuss then sprintf "Enable-Tata-Disscuss-%i" e.DiscussId
        elif e.IsGroup   then sprintf "Enable-Tata-Group-%i" e.GroupId
        else
            failwithf "未知聊天类型！%O" e

    do
        removeRes()
        ws.Options.SetRequestHeader("User-Agent", "CQHttp/4.13.0")
        ws.Options.SetRequestHeader("Authorization", "Token Authorization")
        ws.Options.SetRequestHeader("X-Self-ID", "3084801066")
        ws.Options.SetRequestHeader("X-Client-Role", "Universal")
        ws.ConnectAsync(Uri(url), cts.Token)
        |> Async.AwaitTask
        |> Async.RunSynchronously

        x.StartMessageLoop()
          
    member private x.StartMessageLoop () = 
        x.Logger.Info("正在启动消息处理")
        Tasks.Task.Run(fun () -> 
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

            while (ws.State = WebSocketState.Open) do
                let json = readMessage (new IO.MemoryStream())
                let obj  = JObject.Parse(json)
                x.Logger.Info(sprintf "收到下游WS上报:%s" json)
                let action = obj.["action"].Value<string>()
                let api = {new ApiRequestBase(action) with 
                                member x.WriteParams(w, js) =
                                    let js = obj.["params"].ToString().Trim('{', '}')
                                    w.WriteRaw(js) }
                x.ApiCaller.Value.CallApi(api)
        ) |> ignore

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
                let obj = arg.RawEvent.DeepClone() :?> Newtonsoft.Json.Linq.JObject
                obj.["message"] <- Newtonsoft.Json.Linq.JToken.FromObject(e.RawMessage)
                obj.Property("raw_message").Remove()
                let post = obj.ToString() |> utf8.GetBytes
                let task = ws.SendAsync(ArraySegment<byte>(post), WebSocketMessageType.Text, true, cts.Token)
                task.Wait()
                if task.IsFaulted then
                    x.Logger.Fatal(sprintf "獭獭调用发生异常：%O" task.Exception)

    interface IDisposable with
        member x.Dispose() = 
            ws.Dispose()
            cts.Dispose()