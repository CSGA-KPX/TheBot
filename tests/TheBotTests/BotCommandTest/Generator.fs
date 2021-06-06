module BotCommandTest

open KPX.FsCqHttp.Instance

open Expecto

let GenerateTest () =
    let cmi = ContextModuleInfo()
    DefaultContextModuleLoader().GetModules()
    |> Seq.iter cmi.RegisterModule
    
    cmi.TestCallbacks
    |> Seq.map (fun (name, action) ->
        printfn $"发现测试%s{name}"
        testCase name <| fun _ ->
            action.Invoke())
    |> Seq.toList
    |> testList "BotCommandTest"
    
