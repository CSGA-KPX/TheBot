namespace KPX.TheBot.Host

open System
open System.Collections.Generic
open System.IO

open KPX.FsCqHttp.Instance

open KPX.TheBot.Host.DataCache

open McMaster.NETCore.Plugins


[<AbstractClass>]
/// <summary>
/// 定义需要在模块运行前执行的内容
///
/// 资源检查，LiteDB转换器注册等
///
/// 在HostedModuleDiscover.ProcessType期间执行。不保证具体顺序
/// </summary>
type PluginPrerunInstruction() =
    abstract RunInstructions: unit -> unit

type HostedModuleDiscover() =
    inherit ModuleDiscover()

    let loaders = ResizeArray<PluginLoader>()

    let cacheCols = ResizeArray<Type>()

    let dataTests = ResizeArray<Type>()

    member x.Loaders = loaders :> IReadOnlyList<_>

    member x.CacheCollections = cacheCols :> IReadOnlyList<_>

    member x.CacheCollectionTests = dataTests :> IReadOnlyList<_>

    member x.ScanPlugins(pluginsDir : string) =
        if loaders.Count <> 0 || cacheCols.Count <> 0 then
            invalidOp "不能重复扫描"

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

        if not t.IsAbstract then
            if t.IsSubclassOf(typeof<PluginPrerunInstruction>) then
                if t.GetConstructor(Type.EmptyTypes) <> null then
                    let i = Activator.CreateInstance(t) :?> PluginPrerunInstruction
                    i.RunInstructions()
                    x.Logger.Info($"执行{t.FullName}.RunInstructions()完毕")
                else
                    x.Logger.Info($"跳过类型{t.FullName}：没有无参数构造函数")

            if (typeof<IInitializationInfo>.IsAssignableFrom (t)) then
                cacheCols.Add(t)

            if (typeof<IDataTest>.IsAssignableFrom (t)) then
                dataTests.Add(t)
