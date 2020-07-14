// Learn more about F# at http://fsharp.org
open System
open Mono.Unix
open Mono.Unix.Native
open KPX.FsCqHttp.Instance

let logger = NLog.LogManager.GetCurrentClassLogger()
let mutable loadModules = true

let StartBot(endPoint : string, token : string) =
    let client = CqWebSocketClient(Uri(endPoint), token)
    if loadModules then
        for m in KPX.FsCqHttp.Handler.Utils.GetAllDefinedModules() do
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
    let parser = TheBot.Utils.UserOption.UserOptionParser()
    parser.RegisterOption("rebuilddb", "")
    parser.RegisterOption("debug", "")
    parser.RegisterOption("endpoint", "")
    parser.RegisterOption("token", "")
    parser.Parse(argv)

    if parser.IsDefined("rebuilddb") then
        BotData.Common.Database.BotDataInitializer.ClearCache()
        BotData.Common.Database.BotDataInitializer.ShrinkCache()
        BotData.Common.Database.BotDataInitializer.InitializeAllCollections()
        printfn "Rebuilt Completed"
    elif parser.IsDefined("debug") then
        loadModules <- false
    
    if parser.IsDefined("endpoint") && parser.IsDefined("token") then
        StartBot(parser.GetValue("endpoint"), parser.GetValue("token"))
    else
        printfn "需要定义endpoint和token"
        Console.ReadLine() |> ignore
    0 // return an integer exit code
