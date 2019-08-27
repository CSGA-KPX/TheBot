// Learn more about F# at http://fsharp.org

open System
open cqhttp.Cyan.Messages
open cqhttp.Cyan.Events.CQEvents
open cqhttp.Cyan.Events.CQResponses
open cqhttp.Cyan.Instance
open cqhttp.Cyan

Logger.LogType <- Enums.LogType.Console
Logger.LogLevel <- Enums.Verbosity.ALL

let accessUrl = "wss://coolqapi.danmaku.org"
let token     = "0194caec-12a2-473d-bc08-962049999446"
let eventUrl  = "wss://coolqapi.danmaku.org/event"

let client = new CQWebsocketClient(accessUrl, token, eventUrl)





[<EntryPoint>]
let main argv =
    client.add_OnEventAsync (fun client e ->
        async {
            match e with
            | :? PrivateMessageEvent as x -> 
                //复读机
                //let! ret =  client.SendMessageAsync(Enums.MessageType.private_, x.sender_id, x.message) |> Async.AwaitTask
                return new PrivateMessageResponse(x.message) :> Base.CQResponse

            | :? GroupMessageEvent as x -> 
                //群内复读机
                let sb = new Text.StringBuilder()
                for item in x.message.data do 
                    if item.``type`` = "text" then
                        let text = item :?> CQElements.ElementText
                        sb.Append(text.text) |> ignore
                Logger.Info(sb.ToString())
                if sb.ToString().Contains("...") then
                    let! ret = client.SendMessageAsync(Enums.MessageType.group_, x.group_id, x.message) |> Async.AwaitTask
                    return new GroupMessageResponse(x.message, false) :> Base.CQResponse
                else
                    return new EmptyResponse() :> Base.CQResponse
            | _ ->
                return new EmptyResponse() :> Base.CQResponse
            
        } |> Async.StartAsTask<Base.CQResponse>
    )
    Console.ReadLine() |> ignore
    0 // return an integer exit code
