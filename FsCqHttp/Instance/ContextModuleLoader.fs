namespace KPX.FsCqHttp.Instance

open System
open System.Collections.Generic

open System.Reflection

open KPX.FsCqHttp.Handler


[<AbstractClass>]
type ContextModuleLoader() =
    static let logger = NLog.LogManager.GetCurrentClassLogger()

    static let moduleInstanceCache = ResizeArray<HandlerModuleBase>()

    static do
        seq {
            for asm in AppDomain.CurrentDomain.GetAssemblies() do
                let name = asm.GetName().Name

                if not
                   <| (name = "mscorlib"
                       || name = "netstandard"
                       || name.StartsWith("System.")
                       || name.StartsWith("FSharp.")
                       || name.StartsWith("Microsoft.")) then
                    logger.Info("正在导入程序集：{0}", name)
                    yield! asm.GetTypes()
        }
        |> Seq.filter
            (fun t ->
                t.IsSubclassOf(typeof<HandlerModuleBase>)
                && (not <| t.IsAbstract))
        |> Seq.iter
            (fun t ->
                let i =
                    Activator.CreateInstance(t) :?> HandlerModuleBase

                moduleInstanceCache.Add(i))

    member x.AllDefinedModules = moduleInstanceCache :> IReadOnlyList<_>

    member x.RegisterModuleFor(botUserId : uint64, mi : ContextModuleInfo) =
        for m in x.GetModulesFor(botUserId) do
            logger.Debug("为{0}加载模块{1}", botUserId, m.GetType().FullName)
            mi.AllModules.Add(m)

            if m.OnMeta.IsSome then mi.MetaCallbacks.Add(m.OnMeta.Value)

            if m.OnNotice.IsSome then
                mi.NoticeCallbacks.Add(m.OnNotice.Value)

            if m.OnRequest.IsSome then
                mi.RequestCallbacks.Add(m.OnRequest.Value)

            if m.OnMessage.IsSome then
                mi.MessageCallbacks.Add(m.OnMessage.Value)

            if m :? CommandHandlerBase then
                let cmdBase = m :?> CommandHandlerBase

                for cmd in cmdBase.Commands do
                    mi.Commands.Add(cmd.CommandAttribute.Command, cmd)

    abstract GetModulesFor : botUserId : uint64 -> seq<HandlerModuleBase>

/// 默认加载FsCqHttp项目和EntryAssembly中的所有模块。
type DefaultContextModuleLoader() =
    inherit ContextModuleLoader()

    override x.GetModulesFor(_) = x.AllDefinedModules |> Seq.cast
