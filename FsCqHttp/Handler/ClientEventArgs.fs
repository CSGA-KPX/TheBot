namespace KPX.FsCqHttp.Handler

open System

open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Api

open Newtonsoft.Json.Linq

type ClientEventArgs(api : IApiCallProvider, obj : JObject) =
    inherit EventArgs()

    member val SelfId = obj.["self_id"].Value<uint64>()

    member val Event = Event.CqHttpEvent.FromJObject(obj)

    member x.RawEvent = obj

    member x.ApiCaller = api

    member x.SendResponse(r : Response.EventResponse) =
        if r <> Response.EmptyResponse then
            let rep = SystemApi.QuickOperation(obj.ToString(Newtonsoft.Json.Formatting.None))
            rep.Reply <- r
            api.CallApi(rep) |> ignore

    member x.QuickMessageReply(msg : Message.Message, ?atUser : bool) = 
        let atUser = defaultArg atUser false
        if msg.ToString().Length > KPX.FsCqHttp.Config.Output.TextLengthLimit then
            invalidOp "回复字数超过上限。"
        match x.Event with
        | Event.CqHttpEvent.Message ctx ->
            match ctx with
            | _ when ctx.IsDiscuss -> x.SendResponse(Response.DiscusMessageResponse(msg, atUser))
            | _ when ctx.IsGroup -> x.SendResponse(Response.GroupMessageResponse(msg, atUser, false, false, false, 0))
            | _ when ctx.IsPrivate -> x.SendResponse(Response.PrivateMessageResponse(msg))
            | _ -> raise <| InvalidOperationException("")
        | _ -> raise <| InvalidOperationException("")

    member x.QuickMessageReply(str : string, ?atUser : bool) =
        let msg = new Message.Message()
        msg.Add(str)
        x.QuickMessageReply(msg, defaultArg atUser false)