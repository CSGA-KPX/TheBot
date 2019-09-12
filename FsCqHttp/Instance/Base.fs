namespace KPX.FsCqHttp.Instance.Base
open System
open System.Reflection
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Api

type ClientEventArgs(api : ApiCallManager, context : string, data : Event.EventUnion) = 
    inherit EventArgs()

    let logger   = NLog.LogManager.GetCurrentClassLogger()

    member val ApiManager = api
    member val Data = data
    member val Context = context
    
    member x.CallApi<'T when 'T :> ApiRequestBase>(req : 'T) =
        logger.Trace("Calling {0}", typeof<'T>.Name)
        api.Call<'T>(req)

    /// 调用一个不需要额外设定的api
    member x.CallApi<'T when 'T :> ApiRequestBase and 'T : (new : unit -> 'T)>() =
        let req = Activator.CreateInstance<'T>()
        x.CallApi<'T>(req)

    member x.SendResponse(r : Response.MessageResponse) =
        if r <> Response.EmptyResponse then
            let rep = new SystemApi.QuickOperation(context)
            rep.Reply <- r
            x.CallApi<SystemApi.QuickOperation>(rep) |> ignore

    member x.QuickMessageReply(msg : string, ?atUser : bool) = 
        let atUser = defaultArg atUser false
        match data with
        | Event.EventUnion.Message ctx ->
            let msg = Message.Message.TextMessage(msg)
            match ctx with
            | _ when ctx.IsDiscuss -> x.SendResponse(Response.DiscusMessageResponse(msg, atUser))
            | _ when ctx.IsGroup ->   x.SendResponse(Response.GroupMessageResponse(msg, atUser, false, false, false, 0))
            | _ when ctx.IsPrivate -> x.SendResponse(Response.PrivateMessageResponse(msg))
            | _ -> 
                raise <| InvalidOperationException("")
        | _ -> raise <| InvalidOperationException("")

[<AbstractClass>]
type HandlerModuleBase() as x = 
    let tryToOption (ret, v) = 
        if ret then
            Some(v)
        else
            None

    member val Logger = NLog.LogManager.GetLogger(x.GetType().Name)
    static member SharedConfig = new Collections.Concurrent.ConcurrentDictionary<string, string>()

    abstract MessageHandler : obj -> ClientEventArgs -> unit
    abstract RequestHandler : obj -> ClientEventArgs -> unit
    abstract  NoticeHandler : obj -> ClientEventArgs -> unit

    default x.MessageHandler _ _ = ()
    default x.RequestHandler _ _ = ()
    default  x.NoticeHandler _ _ = ()

    /// 转换名称
    member x.ToNicknameOrCard(msg : Event.Message.MessageEvent) = 
        match msg with
        | msg when msg.IsPrivate -> msg.Sender.NickName
        | msg when msg.IsDiscuss -> msg.Sender.NickName
        | msg when msg.IsGroup -> msg.Sender.Card
        | _ -> failwithf ""

    ///用于访问共享配置
    member x.Item with get (k:string)   = 
                        tryToOption <| HandlerModuleBase.SharedConfig.TryGetValue(k)
                   and set k v =
                        HandlerModuleBase.SharedConfig.AddOrUpdate(k,v,(fun x y -> v)) |> ignore