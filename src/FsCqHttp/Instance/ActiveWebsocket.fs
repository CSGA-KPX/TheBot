namespace KPX.FsCqHttp.Instance

open System
open System.Threading
open System.Net.WebSockets

open KPX.FsCqHttp.Handler


type ActiveWebsocket(url, token) =
    let mutable lastRestart = DateTimeOffset.Now
    let mutable leftRestartCount = 3 + 1
    let mutable context: CqWsContext option = None

    member x.GetContext() =
        if context.IsSome && (leftRestartCount <= 0) then
            failwithf $"ActiveWebsocket:%s{context.Value.BotIdString} 超过重连次数上限"

        if context.IsSome && (DateTimeOffset.Now - lastRestart).TotalMinutes <= 1.0 then
            failwithf $"ActiveWebsocket:%s{context.Value.BotIdString} 重连间隔过短"

        context <- Some(x.StartContext())
        lastRestart <- DateTimeOffset.Now
        leftRestartCount <- leftRestartCount - 1
        context.Value

    member private x.StartContext() =
        let ws = new ClientWebSocket()
        ws.Options.SetRequestHeader("Authorization", $"Bearer %s{token}")

        ws.ConnectAsync(url, CancellationToken())
        |> Async.AwaitTask
        |> Async.RunSynchronously

        let ctx = new CqWsContext(ws)
        // 主动连接情况下需要重启
        ctx.RestartContext <- Some(fun () -> x.GetContext())
        ctx.Start()
        ctx
