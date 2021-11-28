namespace rec KPX.FsCqHttp.Instance

open System
open System.Collections.Generic
open System.Reflection
open System.Threading

open KPX.FsCqHttp
open KPX.FsCqHttp.Handler


type ModuleDiscover() = 
    let modules = ResizeArray<HandlerModuleBase>()
    let logger = NLog.LogManager.GetCurrentClassLogger()

    member x.AllDefinedModules = modules :> IReadOnlyList<_>

    member x.ScanAssembly(asm : Assembly) = 
        let name = asm.GetName().Name

        if not
           <| (name = "mscorlib"
               || name = "netstandard"
               || name.StartsWith("System.")
               || name.StartsWith("FSharp.")
               || name.StartsWith("Microsoft.")) then
            logger.Info("正在导入程序集：{0}", name)

            for t in asm.GetTypes() do
                if t.IsSubclassOf(typeof<HandlerModuleBase>)
                   && (not <| t.IsAbstract) then
                    modules.Add(Activator.CreateInstance(t) :?> HandlerModuleBase)

type LoadedAssemblyDiscover() as x =
    inherit ModuleDiscover()

    do
        for asm in AppDomain.CurrentDomain.GetAssemblies() do 
            x.ScanAssembly(asm)

type ContextModuleLoader(modules : IReadOnlyList<HandlerModuleBase>) = 
    let logger = NLog.LogManager.GetCurrentClassLogger()

    member val AllDefinedModules = modules

    member x.RegisterModuleFor(botUserId : UserId, mi : ContextModuleInfo) =
        for m in x.GetModulesFor(botUserId) do
            logger.Debug("为{0}加载模块{1}", botUserId, m.GetType().FullName)
            mi.RegisterModule(m)

    abstract GetModulesFor : botUserId : UserId -> seq<HandlerModuleBase>
    override x.GetModulesFor _ = x.AllDefinedModules