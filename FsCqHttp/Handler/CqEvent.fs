namespace rec KPX.FsCqHttp.Handler

open System

open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Event

open KPX.FsCqHttp.Api
open KPX.FsCqHttp.Api.System

open KPX.FsCqHttp.Handler

open Newtonsoft.Json.Linq


[<Sealed>]
type EventContext (ctx : JObject) = 
    
    let str = lazy (ctx.ToString(Newtonsoft.Json.Formatting.None))

    member x.Event = ctx

    /// 懒惰求值的字符串
    override x.ToString() = str.Force()

type CqEventArgs(api, ctx) =

    static let logger = NLog.LogManager.GetCurrentClassLogger()

    member internal x.Logger = logger

    member x.ApiCaller : IApiCallProvider = api

    member x.RawEvent : EventContext = ctx

    member x.BotUserId = api.CallerUserId

    member x.BotNickname = api.CallerName

    member x.BotId = api.CallerId

    /// 中断执行过程
    member x.AbortExecution(level : ErrorLevel, fmt : string, [<ParamArray>] args : obj []) : 'T =
        match level with
        | IgnoreError -> raise IgnoreException
        | other ->
            let msg = String.Format(fmt, args)
            let lvl = other.ToString()
            let stack = Diagnostics.StackTrace().ToString()

            x.Logger.Warn(
                "[{0}] -> {1} : {3} \r\n ctx： {2} \r\n stack : {4}",
                x.BotUserId,
                lvl,
                sprintf "%A" x.RawEvent,
                msg,
                stack
            )

            raise IgnoreException

    member x.Reply(r : EventResponse) =
        if r <> EmptyResponse then
            let rep =
                QuickOperation(ctx.ToString())

            rep.Reply <- r
            api.CallApi(rep) |> ignore

    static member Parse(api, ctx : EventContext) =
        match ctx.Event.["post_type"].Value<string>() with
        | "message" ->
            CqMessageEventArgs(api, ctx, ctx.Event.ToObject<MessageEvent>()) :> CqEventArgs
        | "notice" ->
            CqNoticeEventArgs(api, ctx, ctx.Event.ToObject<NoticeEvent>()) :> CqEventArgs
        | "request" ->
            CqRequestEventArgs(api, ctx, ctx.Event.ToObject<RequestEvent>()) :> CqEventArgs
        | "meta_event" -> CqMetaEventArgs(api, ctx, MetaEvent.FromJObject(ctx.Event)) :> CqEventArgs
        | other -> raise <| ArgumentException("未知上报类型：" + other)

type CqMessageEventArgs(api : IApiCallProvider, ctx : EventContext, e) =
    inherit CqEventArgs(api, ctx)

    member x.Event : MessageEvent = e

    member x.AbortExecution(level : ErrorLevel, fmt : string, [<ParamArray>] args : obj []) : 'T =
        x.Reply(String.Format(fmt, args))
        base.AbortExecution(level, fmt, args)

    member x.Reply(msg : Message, ?atUser : bool) =
        let atUser = defaultArg atUser false

        if msg.ToString().Length > KPX.FsCqHttp.Config.Output.TextLengthLimit then
            invalidOp "回复字数超过上限。"

        if x.Event.IsDiscuss then
            x.Reply(DiscusMessageResponse(msg, atUser))
        elif x.Event.IsGroup then
            x.Reply(GroupMessageResponse(msg, atUser, false, false, false, 0))
        elif x.Event.IsPrivate then
            x.Reply(PrivateMessageResponse(msg))
        else
            raise <| InvalidOperationException("")

    member x.Reply(str : string, ?atUser : bool) =
        let msg = new Message()
        msg.Add(str)
        x.Reply(msg, defaultArg atUser false)

type CqNoticeEventArgs(api, ctx, e) =
    inherit CqEventArgs(api, ctx)

    member x.Event : NoticeEvent = e

type CqRequestEventArgs(api, ctx, e) =
    inherit CqEventArgs(api, ctx)

    member x.Event : RequestEvent = e

type CqMetaEventArgs(api, ctx, e) =
    inherit CqEventArgs(api, ctx)

    member x.Event : MetaEvent = e