namespace KPX.FsCqHttp.Instance

open System
open System.Collections.Generic

open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Message.Sections


type ContextModuleInfo() =
    member val AllModules = ResizeArray<HandlerModuleBase>() :> IList<_>

    member val MessageCallbacks = ResizeArray<CqMessageEventArgs -> unit>() :> IList<_>

    member val NoticeCallbacks = ResizeArray<CqNoticeEventArgs -> unit>() :> IList<_>

    member val RequestCallbacks = ResizeArray<CqRequestEventArgs -> unit>() :> IList<_>

    member val MetaCallbacks = ResizeArray<CqMetaEventArgs -> unit>() :> IList<_>

    member val TestCallbacks = ResizeArray<Action>() :> IList<_>

    member val Commands =
        Dictionary<string, CommandInfo>(StringComparer.OrdinalIgnoreCase) :> IDictionary<_, _>

    member x.TryCommand(e : CqMessageEventArgs) =
        e.Event.Message.TryGetSection<TextSection>()
        |> Option.bind
            (fun ts ->
                let key = CommandEventArgs.TryGetCommand(ts.Text)

                if x.Commands.ContainsKey(key) then
                    Some x.Commands.[key]
                else
                    None)

    member x.RegisterModule(m : HandlerModuleBase) =
        x.AllModules.Add(m)

        if m.OnMeta.IsSome then x.MetaCallbacks.Add(m.OnMeta.Value)

        if m.OnNotice.IsSome then
            x.NoticeCallbacks.Add(m.OnNotice.Value)

        if m.OnRequest.IsSome then
            x.RequestCallbacks.Add(m.OnRequest.Value)

        if m.OnMessage.IsSome then
            x.MessageCallbacks.Add(m.OnMessage.Value)

        if m :? CommandHandlerBase then
            let cmdBase = m :?> CommandHandlerBase

            // 添加所有有效指令
            for cmd in cmdBase.Commands do
                x.Commands.Add(cmd.CommandAttribute.Command, cmd)

            let methods =
                cmdBase
                    .GetType()
                    .GetMethods(
                        Reflection.BindingFlags.FlattenHierarchy
                        ||| Reflection.BindingFlags.Public
                        ||| Reflection.BindingFlags.Instance
                    )

            // 添加所有带有TestFixtureAttribute的方法
            for method in methods do
                let attr =
                    method.GetCustomAttributes(typeof<TestFixtureAttribute>, true)

                if attr.Length <> 0 then
                    method.CreateDelegate(typeof<Action>, m) :?> Action
                    |> x.TestCallbacks.Add