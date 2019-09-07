// Learn more about F# at http://fsharp.org

open System
open KPX.TheBot.WebSocket

let accessUrl = "wss://coolqapi.danmaku.org"
let token     = "0194caec-12a2-473d-bc08-962049999446"


let PrivateRepeater _ (arg : ClientEventArgs<CqWebSocketClient, DataType.Event.Message.MessageEvent>) = 
    let msg = arg.Data
    if msg.IsPrivate then
        arg.Response <- DataType.Response.PrivateMessageResponse(msg.Message)

let RequestHandler _ (arg : ClientEventArgs<CqWebSocketClient, DataType.Event.Request.RequestEvent>) = 
    let req = arg.Data
    match req with
    | DataType.Event.Request.FriendRequest x -> ()
    | DataType.Event.Request.GroupRequest x ->
        arg.Response <- DataType.Response.GroupAddResponse(true, "")

[<EntryPoint>]
let main argv =
    let client = new CqWebSocketClient(new Uri(accessUrl), token)
    client.MessageEvent.AddHandler(new Handler<_>(PrivateRepeater))
    client.RequestEvent.AddHandler(new Handler<_>(RequestHandler))
    client.StartListen()

    Console.ReadLine() |> ignore
    0 // return an integer exit code