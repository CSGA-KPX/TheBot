module KPX.TheBot.Program

open System

open KPX.FsCqHttp.Instance
open KPX.FsCqHttp.Utils.UserOption


let logger = NLog.LogManager.GetCurrentClassLogger()

[<EntryPoint>]
let main argv =
    let parser = UserOptionParser()
    parser.RegisterOption("debug", "")
    parser.RegisterOption("endpoint", "")
    parser.RegisterOption("token", "")
    parser.RegisterOption("reverse", "5004")
    parser.Parse(argv)

    if parser.IsDefined("reverse")
       && parser.IsDefined("token") then
        let endpoint =
            sprintf "http://localhost:%i/" (parser.GetValue<int>("reverse"))

        let wss =
            new CqWebSocketServer(endpoint, parser.GetValue("token"))

        wss.Start()
    elif parser.IsDefined("endpoint")
         && parser.IsDefined("token") then
        let uri = Uri(parser.GetValue("endpoint"))
        let token = parser.GetValue("token")
        let aws = ActiveWebsocket(uri, token)
        let ctx = aws.GetContext()
        logger.Info(sprintf "已连接:[%i:%s]" ctx.Self.UserId ctx.Self.Nickname)
        CqWsContextPool.Instance.AddContext(ctx)
    else
        printfn "需要定义endpoint&token或者reverse&token"

    use mtx = new Threading.ManualResetEvent(false)
    AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> mtx.Set() |> ignore)

    mtx.WaitOne() |> ignore

    logger.Info("TheBot已结束。正在关闭WS连接")

    for ws in CqWsContextPool.Instance do
        if ws.CheckOnline() then
            logger.Info(sprintf "向%s发送停止信号" ws.SelfId)
            ws.Stop()
        else
            logger.Error(sprintf "%s已经停止" ws.SelfId)

    Console.ReadLine() |> ignore
    0 // return an integer exit code
