module KPX.TheBot.Program

open System

open KPX.FsCqHttp
open KPX.FsCqHttp.Instance


let logger =
    NLog.LogManager.GetLogger("KPX.TheBot.Program")

[<EntryPoint>]
let main argv =
    let cfgFile =
        IO.Path.Join(KPX.TheBot.Data.Common.Resource.StaticDataPath, "thebot.txt")

    let cfg = FsCqHttpConfigParser()

    if argv.Length <> 0 then
        cfg.Parse(argv)
    elif IO.File.Exists(cfgFile) then
        cfg.Parse(IO.File.ReadAllLines(cfgFile))
    else
        cfg.ParseEnvironment()

    for arg in cfg.DumpDefinedOptions() do
        logger.Info("启动参数：{0}", arg)

    cfg.Start()

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
