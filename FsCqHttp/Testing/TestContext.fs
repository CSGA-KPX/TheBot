namespace rec KPX.FsCqHttp.Testing

open System
open System.Collections.Generic

open KPX.FsCqHttp.Message

open KPX.FsCqHttp.Event

open KPX.FsCqHttp.Api
open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Instance

open Newtonsoft.Json
open Newtonsoft.Json.Linq


module private Strings =
    let msgEvent = """{
   "font":0,
   "message":[],
   "message_id":0,
   "message_type":"private",
   "post_type":"message",
   "raw_message":"",
   "self_id":0,
   "sender":{
      "age":0,
      "nickname":"",
      "sex":"unknown",
      "user_id":0
   },
   "sub_type":"friend",
   "time":0,
   "user_id":0
}"""

exception AssertFailedException of string

type TestContext(m : HandlerModuleBase, ?parent : CqWsContextBase) as x =
    inherit CqWsContextBase()

    static let logger = NLog.LogManager.GetCurrentClassLogger()

    let botUserId = 10000UL
    let botUserName = "测试用户"

    let apiResponse = Dictionary<string, obj>()

    let mutable response = None

    let mutable localRestart = None

    do
        // 用来混淆类型推导机制
        x.AddApiResponse("no_such_command", obj ())
        x.AddApiResponse("can_send_image", {| yes = true |})

        x.Modules.RegisterModule(m)


    member x.AddApiResponse<'T when 'T :> CqHttpApiBase>(obj : obj) =
        let api =
            Activator.CreateInstance(typeof<'T>) :?> CqHttpApiBase

        x.AddApiResponse(api.ActionName, obj)

    member x.AddApiResponse(str, obj) = apiResponse.Add(str, obj)

    member x.InvokeCommand(cmdLine : string) =
        let msg = Message()
        msg.Add(cmdLine)
        x.InvokeCommand(msg)

    member x.InvokeCommand(msg : Message) =
        logger.Info("正在测试 : {0}", msg.ToCqString())
        let msgEvent = x.MakeEvent(msg)

        let cmd = x.Modules.TryCommand(msgEvent)
        if cmd.IsNone then invalidArg "cmdLine/msg" "指定模块不含指定指令"

        let method = cmd.Value.MethodAction
        let attr = cmd.Value.CommandAttribute
        let cmdEvent = CommandEventArgs(msgEvent, attr)

        method.Invoke(cmdEvent)

        response

    member x.ShouldThrow(cmdLine : string) =
        let ret =
            try
                x.InvokeCommand(cmdLine) |> ignore
                false
            with _ -> true

        if not ret then raise <| AssertFailedException "失败：预期捕获异常"

    member x.ShouldNotThrow(cmdLine : string) =
        try
            x.InvokeCommand(cmdLine) |> ignore
        with _ -> reraise ()

    member x.ShouldReturn(cmdLine : string) =
        let ret =
            try
                x.InvokeCommand(cmdLine)
            with _ -> reraise ()

        ret.IsSome

    member x.ReturnContains (cmdLine : string) (value : string) =
        let ret : string option =
            try
                x.InvokeCommand(cmdLine)
            with _ -> reraise ()

        ret.Value.Contains(value)

    member private x.MakeEvent(msg : Message) =
        let raw = JObject.Parse(Strings.msgEvent)

        // 更新Bot信息
        raw.["self_id"] <- JValue(x.BotUserId)

        // 更新发送时间
        raw.["time"] <- JValue(DateTimeOffset.Now.ToUnixTimeSeconds())

        // 更新发送者
        let uid = JValue(x.BotUserId)
        raw.["user_id"] <- uid
        raw.["sender"].["nickname"] <- JValue(x.BotNickname)
        raw.["sender"].["user_id"] <- uid

        // 更新消息Id
        raw.["message_id"] <- uid

        // 更新消息内容
        raw.["message"] <- JToken.FromObject(msg) :?> JArray
        raw.["raw_message"] <- JValue(msg.ToCqString())

        CqEventArgs.Parse(x, EventContext(raw)) :?> CqMessageEventArgs

    override x.RestartContext
        with get () =
            if parent.IsSome then
                parent.Value.RestartContext
            else
                localRestart
        and set (v) =
            if parent.IsSome then
                parent.Value.RestartContext <- v
            else
                localRestart <- v

    override x.IsOnline =
        if parent.IsSome then parent.Value.IsOnline else true

    override x.BotNickname =
        if parent.IsSome then
            parent.Value.BotNickname
        else
            botUserName

    override x.BotUserId =
        if parent.IsSome then parent.Value.BotUserId else botUserId

    override x.BotIdString =
        if parent.IsSome then
            parent.Value.BotIdString
        else
            sprintf "[%i:%s]" x.BotUserId x.BotNickname

    override x.Start() = raise <| NotImplementedException()

    override x.Stop() = raise <| NotImplementedException()

    override x.CallApi(req : #ApiBase) =
        // 拦截所有回复调用
        match req :> ApiBase with
        | :? System.QuickOperation as q ->
            match q.Reply with
            | EventResponse.PrivateMessageResponse msg -> response <- Some <| msg.ToString()
            | _ -> invalidOp "不能处理私聊以外回复"

            req.IsExecuted <- true

        | :? CqHttpApiBase as cqhttp ->
            if apiResponse.ContainsKey(cqhttp.ActionName) then
                let ret =
                    apiResponse.[cqhttp.ActionName]
                    |> JsonConvert.SerializeObject
                    |> JsonConvert.DeserializeObject<Dictionary<string, string>>

                let apiResp =
                    { ApiResponse.ReturnCode = ApiRetCode.OK
                      ApiResponse.DataType = ApiRetType.Object
                      ApiResponse.Echo = Guid.NewGuid().ToString()
                      ApiResponse.Status = "ok"
                      ApiResponse.Data = ret }

                cqhttp.HandleResponse(apiResp)
                req.IsExecuted <- true
            else if parent.IsSome then
                parent.Value.CallApi(req) |> ignore
            else
                invalidOp (sprintf "TestContext 尚未实现API %s" cqhttp.ActionName)
        | :? WsContextApiBase as ctxApi ->
            if parent.IsSome then
                parent.Value.CallApi(ctxApi) |> ignore
            else
                ctxApi.Invoke(x)
                ctxApi.IsExecuted <- true
        | _ ->
            if parent.IsSome then
                parent.Value.CallApi(req) |> ignore
            else
                invalidOp (sprintf "TestContext 尚未实现API %O" (req.GetType().FullName))

        req
