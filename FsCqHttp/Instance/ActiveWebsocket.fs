namespace KPX.FsCqHttp.Instance

open System
open System.Threading
open System.Net.WebSockets

open KPX.FsCqHttp.Handler


type ActiveWebsocket(url, token) =
    let mutable lastRestart = DateTimeOffset.Now
    let mutable leftRestartCount = 3 + 1
    let mutable context : CqWsContext option = None

    member x.GetContext() =
        if context.IsSome && (leftRestartCount <= 0) then failwithf "ActiveWebsocket:%s 超过重连次数上限" context.Value.BotIdString

        if context.IsSome
           && (DateTimeOffset.Now - lastRestart).TotalMinutes
              <= 1.0 then
            failwithf "ActiveWebsocket:%s 重连间隔过短" context.Value.BotIdString

        context <- Some(x.StartContext())
        lastRestart <- DateTimeOffset.Now
        leftRestartCount <- leftRestartCount - 1
        context.Value

    member private x.StartContext() =
        let ws = new ClientWebSocket()
        ws.Options.SetRequestHeader("Authorization", sprintf "Bearer %s" token)

        ws.ConnectAsync(url, new CancellationToken())
        |> Async.AwaitTask
        |> Async.RunSynchronously

        let ctx = new CqWsContext(ws)
        // 主动连接情况下需要重启
        ctx.RestartContext <- Some(fun () -> x.GetContext())
        // 加载所有模块。目前没有写加载方案
        for m in HandlerModuleBase.AllDefinedModules do
            ctx.RegisterModule(m)

        ctx.Start()
        ctx
