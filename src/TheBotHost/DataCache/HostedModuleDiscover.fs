namespace KPX.TheBot.Host

open System
open System.Collections.Generic
open System.IO

open KPX.FsCqHttp.Instance

open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.Testing

open McMaster.NETCore.Plugins


type HostedModuleDiscover() =
    inherit ModuleDiscover()

    let loaders = ResizeArray<PluginLoader>()

    let cacheCols = ResizeArray<Type>()

    let dataTests = ResizeArray<Type>()

    member x.Loaders = loaders :> IReadOnlyList<_>

    member x.CacheCollections = cacheCols :> IReadOnlyList<_>

    member x.CacheCollectionTests = dataTests :> IReadOnlyList<_>

    member x.ScanPlugins() =
        if loaders.Count <> 0 && cacheCols.Count <> 0 then
            invalidOp "不能重复扫描"

        let pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins")

        for dir in Directory.GetDirectories(pluginsDir) do
            let dirName = Path.GetFileName(dir)
            let dll = Path.Combine(dir, dirName + ".dll")

            if File.Exists(dll) then
                let loader = PluginLoader.CreateFromAssemblyFile(dll, (fun cfg -> cfg.PreferSharedTypes <- true))

                loaders.Add(loader)

        for loader in loaders do
            x.ScanAssembly(loader.LoadDefaultAssembly())

    override x.ProcessType(t: Type) =
        base.ProcessType(t)

        if (typeof<IInitializationInfo>.IsAssignableFrom t && (not t.IsAbstract)) then
            cacheCols.Add(t)

        if (typeof<IDataTest>.IsAssignableFrom t && (not t.IsAbstract)) then
            dataTests.Add(t)
