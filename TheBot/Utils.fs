module Utils

(*
///从Message中提取文本
let MessageToText (msg : Message) =
    let sb = new System.Text.StringBuilder()
    for e in msg.data do
        if e.``type`` = "text" then
            sb.Append((e :?> Messages.CQElements.ElementText).text) |> ignore
    sb.ToString()

let StringToMessage (str : string) =
    let elem = new Messages.CQElements.ElementText(str)
    new Message(elem)

[<AbstractClass>]
type ParserBase(p,g,d) =
    abstract Command : string
    member val CanHandlePrivate = p
    member val CanHandleGroup   = g
    member val CanHandleDiscuss = d

    abstract ProcessEvent : string * Base.Sender * Message -> Message

type JrrpParser() =
    inherit ParserBase(true, true, true)

    override x.Command = "jrrp"

    override x.ProcessEvent(arg, sender, message) =
        let seed = sprintf "%i%s" sender.user_id (System.DateTimeOffset.UtcNow.ToOffset(System.TimeSpan.FromHours(8.0)).Date.ToString())
        let jrrp = (seed.GetHashCode() % 100 + 1) |> abs
        StringToMessage(sprintf "%s今天的人品值是：%i" (sender.nickname) jrrp)

type Parser() =
    let parsers =
        [|
            let asm = System.Reflection.Assembly.GetExecutingAssembly()
            for t in asm.DefinedTypes do
                if t.IsSubclassOf(typeof<ParserBase>) && (not <| t.IsAbstract) then
                    Logger.Info(sprintf "已经加载解析器%A" t)
                    let instance = (Activator.CreateInstance(t)) :?> ParserBase
                    yield (instance.Command, instance)
        |] |> readOnlyDict
    let commandStartChar = '/'
    let canHandle (event : Base.CQEvent, parser : #ParserBase) =
        match event with
        | :? PrivateMessageEvent as x ->
            parser.CanHandlePrivate
        | :? GroupMessageEvent as x ->
            parser.CanHandleGroup
        | :? DiscussMessageEvent as x ->
            parser.CanHandleDiscuss
        | _ -> false

    member x.HandleEvent (client : Instance.CQApiClient) (event : Base.CQEvent) =
        async {
            match event with
            | :? Base.MessageEvent as x ->
                let str = MessageToText(x.message)
                if str.StartsWith(commandStartChar) then
                    let cmd =
                        let t = str.Split([|' '|], 2)
                        t.[0].[1..]

                    let reply =
                        if parsers.ContainsKey(cmd) then
                            let p = parsers.[cmd]
                            if canHandle(x, p) then
                                p.ProcessEvent(str, x.sender, x.message)
                            else
                                StringToMessage(sprintf "%s解析器不适用于%s" (cmd) (x.messageType.ToString()))
                        else
                            StringToMessage("命令错误")
                    match x with
                    | :? PrivateMessageEvent as x ->
                        client.SendMessageAsync(Enums.MessageType.private_, x.sender_id, reply)
                        |> Async.AwaitTask |> ignore
                    | :? GroupMessageEvent as x ->
                        client.SendMessageAsync(Enums.MessageType.private_, x.group_id, reply)
                        |> Async.AwaitTask |> ignore
                    | :? DiscussMessageEvent as x ->
                        client.SendMessageAsync(Enums.MessageType.private_, x.discuss_id, reply)
                        |> Async.AwaitTask |> ignore
                    | _ -> ()
                return new EmptyResponse() :> Base.CQResponse
            | :? GroupAddRequestEvent as x ->
                return new GroupAddRequestResponse(true) :> Base.CQResponse
            | _ ->
                return new EmptyResponse() :> Base.CQResponse
        } |> Async.StartAsTask<Base.CQResponse>*)