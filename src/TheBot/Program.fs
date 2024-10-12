module KPX.TheBot.Host.Program

open System
open System.Reflection

open KPX.FsCqHttp
open KPX.FsCqHttp.Instance
open KPX.TheBot.Host.Data


let logger = NLog.LogManager.GetLogger("KPX.TheBot.Program")

[<EntryPoint>]
let main argv =
    let discover = HostedModuleDiscover()
    discover.ScanPlugins(IO.Path.Combine(AppContext.BaseDirectory, "plugins"))
    discover.ScanAssembly(Assembly.GetExecutingAssembly())
    discover.AddModule(KPX.TheBot.Module.DataCacheModule.DataCacheModule(discover))

    let cfg = FsCqHttpConfigParser()
    let cfgFile = DataAgent.GetPersistFile("thebot.txt")

    let runCommand = cfg.RegisterOption<string>("runCommand", "")
    let httpProxy = cfg.RegisterOption<string>("proxy", "")
    let runTest = cfg.RegisterOption("runCmdTest")
    let repl = cfg.RegisterOption("REPL")

    if argv.Length <> 0 then
        cfg.Parse(argv)
    elif IO.File.Exists(cfgFile) then
        cfg.Parse(IO.File.ReadAllLines(cfgFile))
    else
        cfg.ParseEnvironment()

    for arg in cfg.DumpDefinedOptions() do
        logger.Info("启动参数：{0}", arg)

    if httpProxy.IsDefined then
        let proxy = System.Net.WebProxy()
        proxy.Address <- Uri(httpProxy.Value)
        proxy.BypassProxyOnLocal <- true

        Network.TheBotWebFetcher.initHttpClient (Some proxy)

    if runTest.IsDefined then
        try
            let cmi = ContextModuleInfo()
            discover.AllDefinedModules |> Seq.iter cmi.RegisterModule

            for name, action in cmi.TestCallbacks do
                logger.Info($"正在执行{name}")
                action.Invoke()

            0
        with e ->
            logger.Fatal(e)
            1
    else
        if not repl.IsDefined then
            cfg.Start(ContextModuleLoader(discover.AllDefinedModules))

        let ctx =
            let userId =
                Environment.GetEnvironmentVariable("REPL_UserId")
                |> Option.ofObj<string>
                |> Option.map uint64
                |> Option.defaultValue 10000UL
                |> UserId

            let userName =
                Environment.GetEnvironmentVariable("REPL_UserName")
                |> Option.ofObj<string>
                |> Option.defaultValue "测试机"

            Testing.TestContext(discover, userId, userName)

        let runCmd (cmd: string) =
            for msg in ctx.InvokeCommand(cmd) do
                for seg in msg do
                    Console.Out.Write("msg>>")

                    if seg.TypeName = "text" then
                        Console.Out.Write(seg.Values.["text"])
                    else
                        Console.Out.Write($"[{seg.TypeName}]")

                    Console.WriteLine()

        if runCommand.IsDefined then
            runCmd runCommand.Value

        let cmdQueue = ResizeArray<string>()

        while not runCommand.IsDefined do
            if cmdQueue.Count = 0 then
                printf "Command> "

            let line = Console.In.ReadLine()

            if String.IsNullOrWhiteSpace(line) then
                let cmd = String.Join("\r\n", cmdQueue)
                cmdQueue.Clear()

                if not <| String.IsNullOrWhiteSpace(cmd) then
                    try
                        runCmd cmd
                    with e ->
                        printfn $"{e.ToString()}"
            else
                cmdQueue.Add(line)

        0
