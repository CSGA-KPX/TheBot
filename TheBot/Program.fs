module TheBot.Program

open System

open Mono.Unix
open Mono.Unix.Native

open KPX.FsCqHttp.Instance

let logger = NLog.LogManager.GetCurrentClassLogger()

[<EntryPoint>]
let main argv =
    let parser = Utils.UserOption.UserOptionParser()
    parser.RegisterOption("rebuilddb", "")
    parser.RegisterOption("debug", "")
    parser.RegisterOption("endpoint", "")
    parser.RegisterOption("token", "")
    parser.RegisterOption("reverse", "")
    parser.Parse(argv)

    if parser.IsDefined("rebuilddb") then
        BotData.Common.Database.BotDataInitializer.ClearCache()
        BotData.Common.Database.BotDataInitializer.ShrinkCache()
        BotData.Common.Database.BotDataInitializer.InitializeAllCollections()
        printfn "Rebuilt Completed"
    elif parser.IsDefined("debug") then
        KPX.FsCqHttp.Config.Debug.Enable <- true
    
    if parser.IsDefined("reverse") && parser.IsDefined("token") then
        let wss = new CqWebSocketServer("http://127.0.0.1:5010/", parser.GetValue("token"))
        wss.Start()
    elif parser.IsDefined("endpoint") && parser.IsDefined("token") then
        let uri = Uri(parser.GetValue("endpoint"))
        let token = parser.GetValue("token")
        let aws = ActiveWebsocket(uri, token)
        let ctx = aws.GetContext()
        logger.Info(sprintf "已连接:[%i:%s]" ctx.Self.UserId ctx.Self.Nickname)
        CqWsContextPool.Instance.AddContext(ctx)
    else
        printfn "需要定义endpoint&token或者reverse&token"

    if not <| isNull (Type.GetType("Mono.Runtime")) then
        UnixSignal.WaitAny
            ([| new UnixSignal(Signum.SIGINT)
                new UnixSignal(Signum.SIGTERM)
                new UnixSignal(Signum.SIGQUIT)
                new UnixSignal(Signum.SIGHUP) |])
        |> ignore
    else
        Console.ReadLine() |> ignore

    logger.Info("TheBot已结束。正在关闭WS连接")
    for ws in CqWsContextPool.Instance do 
        if ws.CheckOnline() then
            logger.Info(sprintf "向%s发送停止信号" ws.SelfId)
            ws.Stop()
        else    
            logger.Error(sprintf "%s已经停止" ws.SelfId)
    Console.ReadLine() |> ignore
    0 // return an integer exit code
