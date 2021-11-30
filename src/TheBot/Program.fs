module KPX.TheBot.Host.Program

open System
open System.Reflection

open KPX.FsCqHttp
open KPX.FsCqHttp.Instance


let logger = NLog.LogManager.GetLogger("KPX.TheBot.Program")

[<EntryPoint>]
let main argv =
    let discover = HostedModuleDiscover()
    discover.ScanPlugins()

    let cfg = FsCqHttpConfigParser()
    cfg.Parse(argv)

    for arg in cfg.DumpDefinedOptions() do
        logger.Info("启动参数：{0}", arg)

    discover.ScanAssembly(Assembly.GetExecutingAssembly())
    discover.AddModule(KPX.TheBot.Module.DataCacheModule.DataCacheModule(discover))

    cfg.Start(ContextModuleLoader(discover.AllDefinedModules))

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
    0
