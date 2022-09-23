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

    if runTest.IsDefined then
        try
            let cmi = ContextModuleInfo()
            discover.AllDefinedModules |> Seq.iter cmi.RegisterModule

            for name, action in cmi.TestCallbacks do
                logger.Info($"正在执行{name}")
                action.Invoke()

            0
        with
        | e ->
            logger.Fatal(e)
            1
    else
        if not repl.IsDefined then
            cfg.Start(ContextModuleLoader(discover.AllDefinedModules))

        let ctx = Testing.TestContext(discover, UserId UInt64.MaxValue, "REPL")

        while true do
            printf "Command> "
            let cmd = Console.In.ReadToEnd()

            if not <| String.IsNullOrWhiteSpace(cmd) then
                try
                    for msg in ctx.InvokeCommand(cmd) do
                        for seg in msg do
                            Console.Out.Write("msg>>\r\n")

                            if seg.TypeName = "text" then
                                Console.Out.Write(seg.Values.["text"])
                            else
                                Console.Out.Write($"[{seg.TypeName}]")

                            Console.WriteLine()
                with
                | e -> printfn $"{e.ToString()}"

        0
