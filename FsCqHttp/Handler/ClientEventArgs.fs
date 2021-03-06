﻿namespace KPX.FsCqHttp.Handler

open System

open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Event

open KPX.FsCqHttp.Api
open KPX.FsCqHttp.Api.System

open Newtonsoft.Json.Linq

type ErrorLevel =
    | IgnoreError
    | UnknownError
    | InputError
    | ModuleError
    | SystemError
    | ExternalError

    override x.ToString() =
        match x with
        | IgnoreError -> "无视"
        | UnknownError -> "未知错误"
        | InputError -> "输入错误"
        | ModuleError -> "模块错误"
        | SystemError -> "系统错误"
        | ExternalError -> "外部错误"

/// 仅用于中断执行，日志不会记录该异常信息
exception IgnoreException

/// 用于包装ErrorLevel的异常类型
///
/// 用于内部实现代码不方便的调用相关AbortExecution方法时抛出
type ModuleException(level : ErrorLevel, msg : string) =
    inherit Exception(msg)

    new(level : ErrorLevel, fmt : string, [<ParamArray>] args : obj []) =
        ModuleException(level, String.Format(fmt, args))

    member _.ErrorLevel = level

type CqEventArgs private (api : IApiCallProvider, ctx : JObject, selfId, event) =
    inherit EventArgs()

    static let logger = NLog.LogManager.GetCurrentClassLogger()

    new(api : IApiCallProvider, ctx : JObject) =
        let sid = ctx.["self_id"].Value<uint64>()
        let event = CqHttpEvent.FromJObject(ctx)
        CqEventArgs(api, ctx, sid, event)

    new(arg : CqEventArgs) = CqEventArgs(arg.ApiCaller, arg.RawEvent, arg.BotUserId, arg.Event)

    member internal x.Logger = logger

    member val BotUserId : uint64 = selfId

    member val Event : CqHttpEvent = event

    member private x.RawEvent = ctx

    member x.ApiCaller = api

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
                sprintf "%A" x.Event,
                msg,
                stack
            )

            x.QuickMessageReply(sprintf "错误：%s" msg)
            raise IgnoreException

    member x.SendResponse(r : EventResponse) =
        if r <> EmptyResponse then
            let rep =
                QuickOperation(ctx.ToString(Newtonsoft.Json.Formatting.None))

            rep.Reply <- r
            api.CallApi(rep) |> ignore

    member x.QuickMessageReply(msg : Message, ?atUser : bool) =
        let atUser = defaultArg atUser false

        if msg.ToString().Length > KPX.FsCqHttp.Config.Output.TextLengthLimit
        then invalidOp "回复字数超过上限。"

        match x.Event with
        | MessageEvent ctx ->
            match ctx with
            | _ when ctx.IsDiscuss -> x.SendResponse(DiscusMessageResponse(msg, atUser))
            | _ when ctx.IsGroup -> x.SendResponse(GroupMessageResponse(msg, atUser, false, false, false, 0))
            | _ when ctx.IsPrivate -> x.SendResponse(PrivateMessageResponse(msg))
            | _ -> raise <| InvalidOperationException("")
        | _ -> raise <| InvalidOperationException("")

    member x.QuickMessageReply(str : string, ?atUser : bool) =
        let msg = new Message()
        msg.Add(str)
        x.QuickMessageReply(msg, defaultArg atUser false)
