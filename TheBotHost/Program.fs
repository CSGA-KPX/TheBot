open System
open System.IO

open KPX.FsCqHttp.Instance

open McMaster.NETCore.Plugins


let loaders = ResizeArray<PluginLoader>()

let scanPlugins() =
    let pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins")
    for dir in Directory.GetDirectories(pluginsDir) do
        let dirName = Path.GetFileName(dir)
        let dll = Path.Combine(dir, dirName + ".dll")
        if File.Exists(dll) then
            let loader = PluginLoader.CreateFromAssemblyFile(dll, fun cfg -> cfg.PreferSharedTypes <- true)
            loaders.Add(loader)

let scanTypes() = 
    let d = ModuleDiscover()
    for loader in loaders do 
        d.ScanAssembly(loader.LoadDefaultAssembly())
    d.AllDefinedModules