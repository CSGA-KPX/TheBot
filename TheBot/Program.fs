// Learn more about F# at http://fsharp.org

open System
open KPX.TheBot.WebSocket
let accessUrl = "wss://coolqapi.danmaku.org"
let token     = "0194caec-12a2-473d-bc08-962049999446"
let eventUrl  = "wss://coolqapi.danmaku.org/event"

[<EntryPoint>]
let main argv =
    let client = new CqWebSocketClient(new Uri(accessUrl), token)
    client.StartListen()

    Console.ReadLine() |> ignore
    0 // return an integer exit code