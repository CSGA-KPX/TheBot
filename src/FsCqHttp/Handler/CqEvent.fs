namespace rec KPX.FsCqHttp.Handler

open System

open KPX.FsCqHttp
open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Event

open KPX.FsCqHttp.Api
open KPX.FsCqHttp.Api.System

open KPX.FsCqHttp.Handler

open Newtonsoft.Json.Linq


type CqEventArgs internal (api, ctx) =

    static let logger = NLog.LogManager.GetCurrentClassLogger()

    member internal x.Logger = logger

    member x.ApiCaller: IApiCallProvider = api

    member x.RawEvent: PostContent = ctx

    member x.BotUserId = api.ProviderUserId

    member x.BotNickname = api.ProviderName

    member x.BotId = api.ProviderId

    /// <summary>
    /// 中断执行，并记录日志
    /// </summary>
    /// <param name="level">错误级别</param>
    /// <param name="fmt">格式化模板</param>
    /// <param name="args">格式化参数</param>
    member x.Abort(level: ErrorLevel, fmt: string, [<ParamArray>] args: obj []) : 'T =
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

    member x.Reply(r: EventResponse) =
        match r with
        | EmptyResponse -> ()
        | PrivateMessageResponse (uid, reply) ->
            let req = Private.SendPrivateMsg(uid, reply)
            api.CallApi(req) |> ignore
        | GroupMessageResponse (gid, reply) ->
            let req = Group.SendGroupMsg(gid, reply)
            api.CallApi(req) |> ignore
        | FriendAddResponse (_, _)-> raise <| NotImplementedException("TODO")
        | GroupAddResponse (_, _)-> raise <| NotImplementedException("TODO")

    /// <summary>
    /// 根据信息创建对应的CqEventArgs
    /// </summary>
    /// <param name="api">消息提供方的IApiCallProvider</param>
    /// <param name="ctx"></param>
    static member Parse(api, ctx: PostContent) =
        match ctx.RawEventPost.["post_type"].Value<string>() with
        | "message" -> CqMessageEventArgs(api, ctx, ctx.RawEventPost.ToObject<MessageEvent>()) :> CqEventArgs
        | "notice" -> CqNoticeEventArgs(api, ctx, ctx.RawEventPost.ToObject<NoticeEvent>()) :> CqEventArgs
        | "request" -> CqRequestEventArgs(api, ctx, ctx.RawEventPost.ToObject<RequestEvent>()) :> CqEventArgs
        | "meta_event" -> CqMetaEventArgs(api, ctx, MetaEvent.FromJObject(ctx.RawEventPost)) :> CqEventArgs
        | other -> raise <| ArgumentException("未知上报类型：" + other)

    /// <summary>
    /// 根据信息创建对应的CqEventArgs
    /// </summary>
    /// <param name="api">消息提供方的IApiCallProvider</param>
    /// <param name="eventJson">上报事件json</param>
    static member Parse(api, eventJson: string) =
        CqEventArgs.Parse(api, PostContent(JObject.Parse(eventJson)))

type CqMessageEventArgs internal (api: IApiCallProvider, ctx: PostContent, e: MessageEvent) =
    inherit CqEventArgs(api, ctx)

    member x.Event: MessageEvent = e

    /// <summary>
    /// 回复错误信息，并中断执行+记录日志
    /// </summary>
    /// <param name="level">错误级别</param>
    /// <param name="fmt">格式化模板</param>
    /// <param name="args">格式化参数</param>
    member x.Abort(level: ErrorLevel, fmt: string, [<ParamArray>] args: obj []) : 'T =
        x.Reply(String.Format(fmt, args))
        base.Abort(level, fmt, args)

    /// <summary>
    /// 回复消息
    /// </summary>
    /// <param name="msg">OneBot消息</param>
    member x.Reply(msg: ReadOnlyMessage) =
        if msg.ToString().Length > Config.TextLengthLimit then
            invalidOp "回复字数超过上限。"

        x.Reply(x.Event.Response(msg))

    /// <summary>
    /// 回复文本
    /// </summary>
    /// <param name="str">文本信息</param>
    member x.Reply(str: string) =
        let msg = Message()
        msg.Add(str)
        x.Reply(msg)

[<Sealed>]
type CqNoticeEventArgs internal (api, ctx, e) =
    inherit CqEventArgs(api, ctx)

    member x.Event: NoticeEvent = e

[<Sealed>]
type CqRequestEventArgs internal (api, ctx, e) =
    inherit CqEventArgs(api, ctx)

    member x.Event: RequestEvent = e

[<Sealed>]
type CqMetaEventArgs internal (api, ctx, e) =
    inherit CqEventArgs(api, ctx)

    member x.Event: MetaEvent = e
