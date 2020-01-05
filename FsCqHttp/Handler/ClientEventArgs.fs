namespace KPX.FsCqHttp.Handler

open System
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Api
open Newtonsoft.Json.Linq

type ClientEventArgs(api : IApiCallProvider, obj : JObject) =
    inherit EventArgs()

    member val SelfId = obj.["self_id"].Value<uint64>()

    member val Event = Event.EventUnion.From(obj)

    member x.RawEvent = obj

    member x.ApiCaller = api

    member x.SendResponse(r : Response.EventResponse) =
        if r <> Response.EmptyResponse then
            let rep = SystemApi.QuickOperation(obj.ToString(Newtonsoft.Json.Formatting.None))
            rep.Reply <- r
            api.CallApi(rep) |> ignore

    member x.QuickMessageReply(msg : string, ?atUser : bool) =
        let atUser = defaultArg atUser false
        match x.Event with
        | Event.EventUnion.Message ctx when msg.Length >= 3000 -> x.QuickMessageReply("字数太多了，请优化命令或者向管理员汇报bug", true)
        | Event.EventUnion.Message ctx ->
            let msg = Message.Message.TextMessage(msg.Trim())
            match ctx with
            | _ when ctx.IsDiscuss -> x.SendResponse(Response.DiscusMessageResponse(msg, atUser))
            | _ when ctx.IsGroup -> x.SendResponse(Response.GroupMessageResponse(msg, atUser, false, false, false, 0))
            | _ when ctx.IsPrivate -> x.SendResponse(Response.PrivateMessageResponse(msg))
            | _ -> raise <| InvalidOperationException("")
        | _ -> raise <| InvalidOperationException("")