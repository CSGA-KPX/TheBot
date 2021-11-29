namespace rec KPX.FsCqHttp.Instance

open System
open System.Collections.Generic
open System.Reflection
open System.Threading

open KPX.FsCqHttp
open KPX.FsCqHttp.Handler


[<AbstractClass>]
type ModuleDiscover() =
    let modules = ResizeArray<HandlerModuleBase>()
    let logger = NLog.LogManager.GetCurrentClassLogger()

    member x.AllDefinedModules = modules :> IReadOnlyList<_>

    member x.AddModule(m: HandlerModuleBase) = modules.Add(m)

    abstract ProcessType: Type -> unit

    default x.ProcessType(t: Type) =
        if t.IsSubclassOf(typeof<HandlerModuleBase>) && (not <| t.IsAbstract) then
            x.AddModule(Activator.CreateInstance(t) :?> HandlerModuleBase)

    member x.ScanAssembly(asm: Assembly) =
        logger.Info($"正在导入程序集：{asm.GetName().Name}")

        for t in asm.GetTypes() do
            x.ProcessType(t)

type LoadedAssemblyDiscover() as x =
    inherit ModuleDiscover()

    do
        for asm in AppDomain.CurrentDomain.GetAssemblies() do
            let name = asm.GetName().Name

            if not
               <| (name = "mscorlib"
                   || name = "netstandard"
                   || name.StartsWith("System.")
                   || name.StartsWith("FSharp.")
                   || name.StartsWith("Microsoft.")) then
                x.ScanAssembly(asm)

type ContextModuleLoader(modules: IReadOnlyList<HandlerModuleBase>) =
    let logger = NLog.LogManager.GetCurrentClassLogger()

    member val AllDefinedModules = modules

    member x.RegisterModuleFor(botUserId: UserId, mi: ContextModuleInfo) =
        for m in x.GetModulesFor(botUserId) do
            logger.Debug("为{0}加载模块{1}", botUserId, m.GetType().FullName)
            mi.RegisterModule(m)

    abstract GetModulesFor: botUserId: UserId -> seq<HandlerModuleBase>
    override x.GetModulesFor _ = x.AllDefinedModules
