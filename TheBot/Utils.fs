module Utils
open cqhttp.Cyan.Messages
open cqhttp.Cyan.Events.CQEvents
open cqhttp.Cyan.Events.CQResponses
open cqhttp.Cyan

let CommandStarChar = '/'

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
        StringToMessage(sprintf "%s今天的人品值是%i" (sender.nickname) (seed.GetHashCode() % 100 + 1))