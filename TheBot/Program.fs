// Learn more about F# at http://fsharp.org
open System
open Mono.Unix
open Mono.Unix.Native
open KPX.FsCqHttp.Instance

let logger = NLog.LogManager.GetCurrentClassLogger()
let accessUrl = "wss://coolqapi.danmaku.org"
let token     = "0194caec-12a2-473d-bc08-962049999446"

[<EntryPoint>]
let main argv =
    let client = new CqWebSocketClient(new Uri(accessUrl), token)
    for m in KPX.FsCqHttp.Handler.CommandHandlerBase.CommandHandlerBase.AllDefinedModules do 
        logger.Info("正在注册模块{0}", m.GetType().FullName)
        client.RegisterModule(m)
    client.Connect()
    client.StartListen()
    
    let api = new KPX.FsCqHttp.Api.GroupApi.GetGroupMemberInfo(205786383UL, 3084801066UL)
    let test = client.CallApi<KPX.FsCqHttp.Api.GroupApi.GetGroupMemberInfo>(api) 
    printfn "%A" test
    if Type.GetType("Mono.Runtime") <> null then
        UnixSignal.WaitAny(
            [|
                new UnixSignal(Signum.SIGINT)
                new UnixSignal(Signum.SIGTERM)
                new UnixSignal(Signum.SIGQUIT)
                new UnixSignal(Signum.SIGHUP)
            |]) |> ignore
    else
        Console.ReadLine() |> ignore

    client.StopListen()

    Console.WriteLine("Stopping TheBot");
    0 // return an integer exit code