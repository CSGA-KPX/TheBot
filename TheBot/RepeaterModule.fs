module TheBot.Module.RepeaterModule

open System
open System.Collections
open KPX.FsCqHttp.Handler

type RepeatMessage =
    { RawMessage : string
      Count : int }


type XivModule() =
    inherit HandlerModuleBase()
    static let repeatLimit = 5
    static let col = Concurrent.ConcurrentDictionary<uint64, RepeatMessage>()

    override x.HandleMessage(arg, e) =
        if e.IsGroup then
            let gid = e.GroupId
            if col.ContainsKey(gid) && (col.[gid].RawMessage = e.RawMessage) then
                let obj = col.[gid]
                let ret = col.AddOrUpdate(gid, obj, (fun id old -> { old with Count = old.Count + 1 }))
                let isCmd = ret.RawMessage.StartsWith(".") || ret.RawMessage.StartsWith("#")
                if ret.Count = repeatLimit && (not isCmd) then
                    let resp =
                        KPX.FsCqHttp.DataType.Response.GroupMessageResponse(e.Message, false, false, false, false, 0)
                    arg.SendResponse(resp)
            else
                let obj =
                    { RawMessage = e.RawMessage
                      Count = 1 }
                col.AddOrUpdate(gid, obj, (fun _ _ -> obj)) |> ignore
