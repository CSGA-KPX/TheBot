module TheBot.Module.TataruBridgeModule
(*
open System
open KPX.FsCqHttp.Api
open System.Net.Http
open KPX.FsCqHttp.Handler.CommandHandlerBase
open TheBot.Utils.Config

type MessageEvent = KPX.FsCqHttp.DataType.Event.Message.MessageEvent

type TataruReply(context : string, json : string) =
    inherit ApiRequestBase(".handle_quick_operation")

    override x.WriteParams(w, _) =
        w.WritePropertyName("context")
        w.WriteRawValue(context)
        w.WritePropertyName("operation")
        w.WriteRawValue(json)

type TataruBridgeModule() = 
    inherit CommandHandlerBase()

    let TataruCommandStart = "/"
    let API_KEY = "http://tataru.aoba.vip/api/cq_http_api.php?key=b29992910c0f22333576f83cef626f57"

    let hc = new HttpClient(Timeout = TimeSpan.FromSeconds(20.0))

    let cm = ConfigManager(ConfigOwner.System)

    let getKey(e : MessageEvent) = 
        if   e.IsPrivate then failwith "匿名总是允许的！"
        elif e.IsDiscuss then sprintf "Disscuss-%i" e.DiscussId
        elif e.IsGroup   then sprintf "Group-%i" e.GroupId
        else
            failwithf "未知聊天类型！%O" e

    [<CommandHandlerMethodAttribute("#tataru", "(群管理) 允许/拒绝桥接塔塔露bot", "")>]
    member x.HandleTataruCmd(msgArg : CommandArgs) = 
        let key = getKey(msgArg.MessageEvent)
        let now = cm.Get<bool>(key, false) |> not
        cm.Put(key, now)
        if now then
            msgArg.QuickMessageReply("已启用塔塔露桥接")
        else
            msgArg.QuickMessageReply("已禁用塔塔露桥接")

    override x.HandleMessage(arg, e) =
        base.HandleMessage(arg, e)

        if e.Message.ToString().StartsWith(TataruCommandStart) then
            let allow = e.IsPrivate || cm.Get<bool>(getKey(e), false)
            if allow then
                let obj = arg.RawEvent
                obj.["message"] <- Newtonsoft.Json.Linq.JToken.FromObject(e.RawMessage)
                obj.Property("raw_message").Remove()
                let json = arg.RawEvent.ToString(Newtonsoft.Json.Formatting.None)
                use content = new StringContent(json, Text.Encoding.UTF8, "application/json")
                content.Headers.Add("X-Self-ID", arg.SelfId.ToString())
                let ret = 
                    hc.PostAsync(API_KEY, content)
                      .ConfigureAwait(false)
                      .GetAwaiter()
                      .GetResult()
                if ret.StatusCode <> Net.HttpStatusCode.OK then
                    let err = sprintf "访问塔塔露失败：%O" ret
                    x.Logger.Fatal(err)
                    failwith err
                let retJson = ret.Content.ReadAsStringAsync().Result
                x.Logger.Fatal(retJson)
                let reply = TataruReply(json, retJson)
                arg.CallApi(reply)
                ()
            ()*)