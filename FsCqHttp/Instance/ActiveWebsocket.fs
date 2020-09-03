namespace KPX.FsCqHttp.Instance

open System
open System.Collections.Generic
open System.Threading
open System.Net.WebSockets

open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.Handler

open Newtonsoft.Json.Linq

type ActiveWebsocket(url, token) = 
    
    member x.StartContext() = 
        let ws = new ClientWebSocket()
        ws.Options.SetRequestHeader("Authorization", sprintf "Bearer %s" token)
        ws.ConnectAsync(url, new CancellationToken())
        |> Async.AwaitTask
        |> Async.RunSynchronously

        let ctx = new CqWsContext(ws)
        // 加载所有模块。目前没有写加载方案
        for m in HandlerModuleBase.AllDefinedModules do 
            ctx.RegisterModule(m)
        ctx.Start()
        ctx