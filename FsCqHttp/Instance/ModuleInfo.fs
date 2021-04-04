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

    member val Commands =
        Dictionary<string, CommandInfo>(StringComparer.OrdinalIgnoreCase) :> IDictionary<_, _>
    
    member x.TryCommand (e : CqMessageEventArgs) = 
        e.Event.Message.TryGetSection<TextSection>()
        |> Option.bind (fun ts -> 
            let key = CommandEventArgs.TryGetCommand(ts.Text)
            if x.Commands.ContainsKey(key) then
                Some x.Commands.[key]
            else
                None
        )