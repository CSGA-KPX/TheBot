module KPX.FsCqHttp.Handler.Utils

open System.Reflection
open KPX.FsCqHttp.Handler

let AllDefinedModules =
    [| yield! Assembly.GetExecutingAssembly().GetTypes()
       yield! Assembly.GetEntryAssembly().GetTypes() |]
    |> Array.filter (fun t -> t.IsSubclassOf(typeof<HandlerModuleBase>) && (not <| t.IsAbstract))
