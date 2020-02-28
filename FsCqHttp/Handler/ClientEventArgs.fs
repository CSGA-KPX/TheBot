namespace KPX.FsCqHttp.Handler

open System
open System.Collections.Generic
open System.IO
open System.Text
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Api
open Newtonsoft.Json.Linq

type ClientEventArgs(api : IApiCallProvider, obj : JObject) =
    inherit EventArgs()

    member val SelfId = obj.["self_id"].Value<uint64>()

    member val Event = Event.EventUnion.From(obj)

    member x.RawEvent = obj

    member x.ApiCaller = api

    member x.SendResponse(r : Response.EventResponse) =
        if r <> Response.EmptyResponse then
            let rep = SystemApi.QuickOperation(obj.ToString(Newtonsoft.Json.Formatting.None))
            rep.Reply <- r
            api.CallApi(rep) |> ignore

    member x.QuickMessageReply(msg : string, ?atUser : bool) =
        let atUser = defaultArg atUser false
        match x.Event with
        | Event.EventUnion.Message ctx when msg.Length > 3000 -> x.QuickMessageReply("字数太多了，请优化命令或者向管理员汇报bug", true)
        | Event.EventUnion.Message ctx ->
            let msg = Message.Message.TextMessage(msg.Trim())
            match ctx with
            | _ when ctx.IsDiscuss -> x.SendResponse(Response.DiscusMessageResponse(msg, atUser))
            | _ when ctx.IsGroup -> x.SendResponse(Response.GroupMessageResponse(msg, atUser, false, false, false, 0))
            | _ when ctx.IsPrivate -> x.SendResponse(Response.PrivateMessageResponse(msg))
            | _ -> raise <| InvalidOperationException("")
        | _ -> raise <| InvalidOperationException("")

    member x.OpenResponse() = new TextResponse(x)

and TextResponse(arg : ClientEventArgs) = 
    inherit TextWriter()

    let logger = NLog.LogManager.GetCurrentClassLogger()
    let sizeLimit = 3000
    let buf = Queue<string>()
    let sb = StringBuilder()
    
    override x.Write(c:char) = 
        sb.Append(c) |> ignore

    override x.WriteLine() = 
        buf.Enqueue(sb.ToString())
        sb.Clear() |> ignore

    // .NET自带的是另一套算法，这里强制走Write(string)
    override x.WriteLine(str : string) = 
        x.Write(str)
        x.WriteLine()

    override x.Encoding = Encoding.Default

    override x.ToString() = 
        String.Join(x.NewLine, buf)

    override x.Flush() = 
        if sb.Length <> 0 then
            x.WriteLine()

        let pages = 
            [|
                let sb = StringBuilder()

                while buf.Count <> 0 do
                    let peek = buf.Dequeue()
                    if sb.Length + peek.Length > sizeLimit then
                        yield sb.ToString()
                        sb.Clear() |> ignore
                    sb.AppendLine(peek) |> ignore

                if sb.Length <> 0 then
                    yield sb.ToString()
            |]
        logger.Info("{0} Pages!", pages.Length)
        for page in pages do 
            logger.Info("{0}", page)
            arg.QuickMessageReply(page, false)

    interface IDisposable with
        member x.Dispose() = 
            logger.Info("Dispose() Called!")
            base.Dispose()
            x.Flush()
            