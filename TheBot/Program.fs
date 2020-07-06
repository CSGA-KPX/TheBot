// Learn more about F# at http://fsharp.org
open System
open Mono.Unix
open Mono.Unix.Native
open KPX.FsCqHttp.Instance

let logger = NLog.LogManager.GetCurrentClassLogger()
let accessUrl = "wss://coolqapi.danmaku.org"
let token = "0194caec-12a2-473d-bc08-962049999446"
let mutable loadModules = true

let StartBot() =
    //KPX.FsCqHttp.Handler.CommandHandlerBase.CommandHandlerMethodAttribute.CommandStart <- "#!"
    let client = CqWebSocketClient(Uri(accessUrl), token)
    let ms = KPX.FsCqHttp.Handler.Utils.AllDefinedModules
    if loadModules then
        for m in ms do
            logger.Info("正在注册模块{0}", m.FullName)
            client.RegisterModule(m)
    else
        logger.Info("启动观测者模式")

    client.Connect()
    client.StartListen()

    if not <| isNull (Type.GetType("Mono.Runtime")) then
        UnixSignal.WaitAny
            ([| new UnixSignal(Signum.SIGINT)
                new UnixSignal(Signum.SIGTERM)
                new UnixSignal(Signum.SIGQUIT)
                new UnixSignal(Signum.SIGHUP) |])
        |> ignore
    else
        Console.ReadLine() |> ignore

    client.StopListen()
    Console.WriteLine("Stopping TheBot")

[<EntryPoint>]
let main argv =
    if argv.Length <> 0 && argv.[0].ToLowerInvariant() = "rebuilddb" then
        BotData.Common.Database.BotDataInitializer.ClearCache()
        BotData.Common.Database.BotDataInitializer.ShrinkCache()
        BotData.Common.Database.BotDataInitializer.InitializeAllCollections()
        printfn "Rebuilt Completed"
    elif  argv.Length <> 0 && argv.[0].ToLowerInvariant() = "ob" then
        loadModules <- false
    StartBot()
    0 // return an integer exit code
