module RepeaterModule
open System
open System.Collections
open KPX.FsCqHttp.Handler.Base




type RepeatMessage = 
    {
        RawMessage  : string
        Count    : int
    }


type XivModule() = 
    inherit HandlerModuleBase()
    static let repeatLimit = 3
    static let col = new Concurrent.ConcurrentDictionary<uint64, RepeatMessage>()

    override x.HandleMessage(arg, e) = 
        if e.IsGroup then
            let gid = e.GroupId
            if col.ContainsKey(gid) && (col.[gid].RawMessage = e.RawMessage) then
                let obj = col.[gid]
                let ret = col.AddOrUpdate(gid, obj, (fun id old  -> {old with Count = old.Count + 1}))
                if ret.Count = repeatLimit then
                    let resp = 
                        KPX.FsCqHttp.DataType.Response.GroupMessageResponse(e.Message, false, false, false, false, 0)
                    arg.SendResponse(resp)
            else
                let obj = 
                    {
                        RawMessage = e.RawMessage
                        Count      = 0
                    }
                col.AddOrUpdate(gid, obj, (fun _ _  -> obj)) |> ignore