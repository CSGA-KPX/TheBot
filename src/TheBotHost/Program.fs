open System
open System.IO

open KPX.FsCqHttp.Instance

open McMaster.NETCore.Plugins
open KPX.FsCqHttp


let loaders = ResizeArray<PluginLoader>()

let scanPlugins() =
    let pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins")
    for dir in Directory.GetDirectories(pluginsDir) do
        let dirName = Path.GetFileName(dir)
        let dll = Path.Combine(dir, dirName + ".dll")
        printfn $"发现{dll}"
        if File.Exists(dll) then
            let loader = PluginLoader.CreateFromAssemblyFile(dll, fun cfg -> cfg.PreferSharedTypes <- true)
            loaders.Add(loader)

let scanTypes() = 
    let d = ModuleDiscover()
    for loader in loaders do 
        d.ScanAssembly(loader.LoadDefaultAssembly())
    d.AllDefinedModules

let logger =
    NLog.LogManager.GetLogger("KPX.TheBot.Program")


[<EntryPoint>]
let main argv = 
    scanPlugins()
    let types = scanTypes()
    for t in types do 
        printfn $"{t.GetType().FullName}"

    let cfg = FsCqHttpConfigParser()
    cfg.Parse(argv)

    for arg in cfg.DumpDefinedOptions() do
        logger.Info("启动参数：{0}", arg)
    
    cfg.Start(ContextModuleLoader(types))

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