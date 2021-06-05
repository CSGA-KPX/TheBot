namespace KPX.FsCqHttp.Api.System

open KPX.FsCqHttp.Event
open KPX.FsCqHttp.Api


/// 快速操作
type QuickOperation(context : string) =
    inherit CqHttpApiBase(".handle_quick_operation")

    member val Reply = EmptyResponse with get, set

    override x.WriteParams(w, js) =
        w.WritePropertyName("context")
        w.WriteRawValue(context)
        w.WritePropertyName("operation")
        w.WriteStartObject()
        js.Serialize(w, x.Reply)
        w.WriteEndObject()
