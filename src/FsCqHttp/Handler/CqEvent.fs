namespace rec KPX.FsCqHttp.Handler

open System

open KPX.FsCqHttp
open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Event

open KPX.FsCqHttp.Api
open KPX.FsCqHttp.Api.System

open KPX.FsCqHttp.Handler

open Newtonsoft.Json.Linq


type CqEventArgs(api, ctx) =

    static let logger = NLog.LogManager.GetCurrentClassLogger()

    member internal x.Logger = logger

    member x.ApiCaller : IApiCallProvider = api

    member x.RawEvent : PostContent = ctx

    member x.BotUserId = api.ProviderUserId

    member x.BotNickname = api.ProviderName

    member x.BotId = api.ProviderId

    /// 中断执行过程
    member x.Abort(level : ErrorLevel, fmt : string, [<ParamArray>] args : obj []) : 'T =
        match level with
        | IgnoreError -> raise IgnoreException
        | other ->
            let msg = String.Format(fmt, args)
            let lvl = other.ToString()
            let stack = Diagnostics.StackTrace().ToString()

            x.Logger.Warn(
                "[{0}] -> {1} : {3} \r\n ctx： \r\n{2} \r\n stack : \r\n{4}",
                x.BotUserId,
                lvl,
                $"%A{x.RawEvent}",
                msg,
                stack
            )

            raise IgnoreException

    member x.Reply(r : EventResponse) =
        if r <> EmptyResponse then
            let rep =
                QuickOperation(ctx)

            rep.Reply <- r
            api.CallApi(rep) |> ignore

    static member Parse(api, ctx : PostContent) =
        match ctx.RawEventPost.["post_type"].Value<string>() with
        | "message" ->
            CqMessageEventArgs(api, ctx, ctx.RawEventPost.ToObject<MessageEvent>()) :> CqEventArgs
        | "notice" ->
            CqNoticeEventArgs(api, ctx, ctx.RawEventPost.ToObject<NoticeEvent>()) :> CqEventArgs
        | "request" ->
            CqRequestEventArgs(api, ctx, ctx.RawEventPost.ToObject<RequestEvent>()) :> CqEventArgs
        | "meta_event" -> CqMetaEventArgs(api, ctx, MetaEvent.FromJObject(ctx.RawEventPost)) :> CqEventArgs
        | other -> raise <| ArgumentException("未知上报类型：" + other)
    
    static member Parse(api, eventJson : string) =
        CqEventArgs.Parse(api, PostContent(JObject.Parse(eventJson)))
        
type CqMessageEventArgs(api : IApiCallProvider, ctx : PostContent, e : MessageEvent) =
    inherit CqEventArgs(api, ctx)

    member x.Event : MessageEvent = e

    member x.Abort(level : ErrorLevel, fmt : string, [<ParamArray>] args : obj []) : 'T =
        x.Reply(String.Format(fmt, args))
        base.Abort(level, fmt, args)

    member x.Reply(msg : ReadOnlyMessage) =
        if msg.ToString().Length > Config.TextLengthLimit then
            invalidOp "回复字数超过上限。"
        
        x.Reply(x.Event.Response(msg))

    member x.Reply(str : string) =
        let msg = Message()
        msg.Add(str)
        x.Reply(msg)

type CqNoticeEventArgs(api, ctx, e) =
    inherit CqEventArgs(api, ctx)

    member x.Event : NoticeEvent = e

type CqRequestEventArgs(api, ctx, e) =
    inherit CqEventArgs(api, ctx)

    member x.Event : RequestEvent = e

type CqMetaEventArgs(api, ctx, e) =
    inherit CqEventArgs(api, ctx)

    member x.Event : MetaEvent = e