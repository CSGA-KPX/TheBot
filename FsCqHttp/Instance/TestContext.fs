namespace rec KPX.FsCqHttp.Instance.Testing

open System
open System.Collections.Generic

open KPX.FsCqHttp.Message

open KPX.FsCqHttp.Event

open KPX.FsCqHttp.Api
open KPX.FsCqHttp.Handler

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

type TestContext() as x =

    let botUserId = 10000UL
    let botUserName = "TestContext"

    let apiResponse = Dictionary<string, obj>()

    let responseQueue = Queue<string>()

    do
        x.AddApiResponse(".handle_quick_operation", obj ())

        x.AddApiResponse("can_send_image", {| yes = true |})

    member x.AddApiResponse<'T when 'T :> CqHttpApiBase>(obj : obj) =
        let api =
            Activator.CreateInstance(typeof<'T>) :?> CqHttpApiBase

        x.AddApiResponse(api.ActionName, obj)

    member x.AddApiResponse(str, obj) = apiResponse.Add(str, obj)

    // TODO :
    // 一个method会有多个指令名
    // 应该是 x.InvokeCommand(instance : CommandHandlerBase, cmd : string)
    // 拆分cmd以后去比对相关方法
    member x.InvokeCommand(instance : CommandHandlerBase, methodName : string, ?args : string) =
        let method =
            instance
                .GetType()
                .GetMethod(methodName, Array.singleton typeof<CommandEventArgs>)

        let attr =
            method.GetCustomAttributes(typeof<CommandHandlerMethodAttribute>, true)
            |> Array.head
            :?> CommandHandlerMethodAttribute

        let msg = Message()
        msg.Add(attr.Command + " " + (defaultArg args ""))
        let msgEvent = x.MakeEvent(msg)
        let cmdEvent = CommandEventArgs(msgEvent, attr)

        method.Invoke(instance, Array.singleton<obj> cmdEvent)
        |> ignore

        responseQueue.Dequeue()

    member x.MakeEvent(msg : Message) =
        let raw = JObject.Parse(Strings.msgEvent)

        // 更新Bot信息
        raw.["self_id"] <- JValue(botUserId)

        // 更新发送时间
        raw.["time"] <- JValue(DateTimeOffset.Now.ToUnixTimeSeconds())

        // 更新发送者
        let uid = x.GetUserId()
        raw.["user_id"] <- JValue(uid)
        raw.["sender"].["nickname"] <- JValue(sprintf "测试用户%i" uid)
        raw.["sender"].["user_id"] <- JValue(uid)

        // 更新消息Id
        raw.["message_id"] <- JValue(uid)

        // 更新消息内容
        raw.["message"] <- JToken.FromObject(msg) :?> JArray
        raw.["raw_message"] <- JValue(msg.ToCqString())

        CqEventArgs.Parse(x, EventContext(raw)) :?> CqMessageEventArgs

    member private x.GetUserId() : uint64 =
        DateTimeOffset.Now.ToUnixTimeMilliseconds()
        |> uint64

    interface IApiCallProvider with
        member x.CallerId = ""
        member x.CallerUserId = botUserId
        member x.CallerName = botUserName

        member x.CallApi(req : #ApiBase) =
            // 拦截所有回复调用

            match req :> ApiBase with
            | :? System.QuickOperation as q ->
                match q.Reply with
                | EventResponse.PrivateMessageResponse msg ->
                    msg.ToString() |> responseQueue.Enqueue

                | _ -> invalidOp "咱不能处理私聊以外回复"
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
                else
                    invalidOp (sprintf "尚未实现API %s" cqhttp.ActionName)
            | _ -> invalidOp (sprintf "尚未实现API %O" (req.GetType().FullName))

            req.IsExecuted <- true

            req

        member x.CallApi<'T when 'T :> ApiBase and 'T : (new : unit -> 'T)>() =
            let req = Activator.CreateInstance<'T>()
            (x :> IApiCallProvider).CallApi(req)
