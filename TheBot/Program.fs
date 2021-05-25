module KPX.TheBot.Program

open System

open KPX.FsCqHttp.Instance
open KPX.FsCqHttp.Utils.UserOption


let logger = NLog.LogManager.GetCurrentClassLogger()

[<EntryPoint>]
let main argv =
    let cfg = OptionImpl()

    let endpoint = cfg.RegisterOption("endpoint", "")
    let token = cfg.RegisterOption("token", "")
    let reverse = cfg.RegisterOption("reverse", 5004)

    cfg.Parse(argv)

    if reverse.IsDefined && token.IsDefined then
        let endpoint =
            $"http://localhost:%i{reverse.Value}/"

        let wss =
            new CqWebSocketServer(endpoint, token.Value)

        wss.Start()
    elif endpoint.IsDefined && token.IsDefined then
        let uri = Uri(endpoint.Value)
        let token = token.Value
        let aws = ActiveWebsocket(uri, token)
        let ctx = aws.GetContext()
        logger.Info $"已连接:[%i{ctx.BotUserId}:%s{ctx.BotNickname}]"
        CqWsContextPool.Instance.AddContext(ctx)
    else
        printfn "需要定义endpoint&token或者reverse&token"

    use mtx = new Threading.ManualResetEvent(false)
    AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> mtx.Set() |> ignore)

    mtx.WaitOne() |> ignore

    logger.Info("TheBot已结束。正在关闭WS连接")

    for ws in CqWsContextPool.Instance do
        if ws.IsOnline then
            logger.Info $"向%s{ws.BotIdString}发送停止信号"
            ws.Stop()
        else
            logger.Error $"%s{ws.BotIdString}已经停止"

    Console.ReadLine() |> ignore
    0 // return an integer exit code
