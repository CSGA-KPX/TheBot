namespace KPX.FsCqHttp.Api

open System.Collections.Generic
open System
open System.Threading
open System.Net.WebSockets
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Api

///用于管理需要处理Echo的API调用
type ApiCallManager(ws : ClientWebSocket, token : CancellationToken) =
    let logger = NLog.LogManager.GetCurrentClassLogger()
    let utf8 = Text.Encoding.UTF8
    let getEcho() = (Guid.NewGuid().ToString())
    let pendingApi = Dictionary<string, ManualResetEvent * ApiRequestBase>()
    let lock = new ReaderWriterLockSlim()

    /// 调用API并等待结果
    member x.Call<'T when 'T :> ApiRequestBase>(req : ApiRequestBase) =
        async {
            let echo = getEcho()
            let mre = new ManualResetEvent(false)
            let json = req.GetRequestJson(echo)

            lock.EnterWriteLock()
            pendingApi.Add(echo, (mre, req)) |> ignore
            lock.ExitWriteLock()

            logger.Trace("请求API：{0}", json)
            let data = json |> utf8.GetBytes
            do! ws.SendAsync(ArraySegment<byte>(data), WebSocketMessageType.Text, true, token) |> Async.AwaitTask
            let! ret = Async.AwaitWaitHandle(mre :> WaitHandle)

            lock.EnterWriteLock()
            pendingApi.Remove(echo) |> ignore
            lock.ExitWriteLock()
        }
        |> Async.RunSynchronously
        req :?> 'T

    /// 处理ApiResponse
    /// 根据Echo让对应调用处理结果
    member x.HandleResponse(ret : Response.ApiResponse) =
        logger.Trace("收到API调用结果：{0}", sprintf "%A" ret)
        lock.EnterReadLock()
        let notEmpty = not <| String.IsNullOrEmpty(ret.Echo)
        let hasPending = pendingApi.ContainsKey(ret.Echo)
        if notEmpty && hasPending then
            logger.Trace("Passing {0}", ret.Echo)
            let (mre, api) = pendingApi.[ret.Echo]
            api.HandleResponse(ret)
            mre.Set() |> ignore
        lock.ExitReadLock()
