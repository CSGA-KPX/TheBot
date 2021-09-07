module Program

open System

open Expecto




[<Tests>]
let BotDataTestGroup =
    let logTarget = new NLog.Targets.ColoredConsoleTarget()
    NLog.Config.SimpleConfigurator.ConfigureForTargetLogging(logTarget, NLog.LogLevel.Trace)
    
    Environment.CurrentDirectory <- IO.Path.Join(__SOURCE_DIRECTORY__, "../../build/test/")
    
    // 一个强制到TheBot的引用以便加载程序集
    KPX.TheBot.Program.logger |> ignore
    
    testSequencedGroup "MainTestGroup"
    <| testList
        "MainTestGroupList"
        [ yield BotCommandTest.GenerateTest() ]

[<EntryPoint>]
let main argv =
    Tests.runTestsInAssemblyWithCLIArgs [] argv
