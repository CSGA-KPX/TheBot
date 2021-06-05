namespace KPX.FsCqHttp.Instance

open System
open System.Collections.Generic

open System.Threading

open KPX.FsCqHttp.Handler


[<AbstractClass>]
type ContextModuleLoader() =
    static let logger = NLog.LogManager.GetCurrentClassLogger()

    static let cacheBuiltEvent = new ManualResetEvent(false)

    static let moduleInstanceCache = ResizeArray<HandlerModuleBase>()

    static do
        for asm in AppDomain.CurrentDomain.GetAssemblies() do
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
                        moduleInstanceCache.Add(Activator.CreateInstance(t) :?> HandlerModuleBase)

        cacheBuiltEvent.Set() |> ignore

    static member CacheBuiltEvent = cacheBuiltEvent

    member x.AllDefinedModules = moduleInstanceCache :> IReadOnlyList<_>

    member x.RegisterModuleFor(botUserId : uint64, mi : ContextModuleInfo) =
        for m in x.GetModulesFor(botUserId) do
            logger.Debug("为{0}加载模块{1}", botUserId, m.GetType().FullName)
            mi.RegisterModule(m)

    abstract GetModulesFor : botUserId : uint64 -> seq<HandlerModuleBase>

/// 默认加载FsCqHttp项目和EntryAssembly中的所有模块。
type DefaultContextModuleLoader() =
    inherit ContextModuleLoader()

    override x.GetModulesFor _ = x.AllDefinedModules |> Seq.cast
