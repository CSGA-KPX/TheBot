namespace KPX.FsCqHttp.Handler.Base

open System
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Api
open Newtonsoft.Json.Linq

type ClientEventArgs(api : IApiCallProvider, obj : JObject) =
    inherit EventArgs()

    let logger = NLog.LogManager.GetCurrentClassLogger()

    member val SelfId = obj.["self_id"].Value<uint64>()

    member val Event = Event.EventUnion.From(obj)

    member x.CallApi(req) =
        logger.Trace("Calling {0}", req.GetType().Name)
        api.CallApi(req)

    /// 调用一个不需要额外设定的api
    member x.CallApi<'T when 'T :> ApiRequestBase and 'T : (new : unit -> 'T)>() =
        let req = Activator.CreateInstance<'T>()
        x.CallApi(req)
        req

    member x.SendResponse(r : Response.EventResponse) =
        if r <> Response.EmptyResponse then
            let rep = SystemApi.QuickOperation(obj.ToString(Newtonsoft.Json.Formatting.None))
            rep.Reply <- r
            x.CallApi(rep) |> ignore

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

[<AbstractClass>]
type HandlerModuleBase() as x =

    member val Logger = NLog.LogManager.GetLogger(x.GetType().Name)

    abstract HandleMessage : ClientEventArgs * Event.Message.MessageEvent -> unit
    abstract HandleRequest : ClientEventArgs * Event.Request.RequestEvent -> unit
    abstract HandleNotice : ClientEventArgs * Event.Notice.NoticeEvent -> unit

    default x.HandleMessage(_, _) = ()
    default x.HandleRequest(_, _) = ()
    default x.HandleNotice(_, _) = ()

    abstract HandleCqHttpEvent : obj -> ClientEventArgs -> unit
    default x.HandleCqHttpEvent _ args =
        match args.Event with
        | Event.EventUnion.Message y -> x.HandleMessage(args, y)
        | Event.EventUnion.Request y -> x.HandleRequest(args, y)
        | Event.EventUnion.Notice y -> x.HandleNotice(args, y)
        | _ -> ()
