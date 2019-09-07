// Learn more about F# at http://fsharp.org

open System
open KPX.TheBot.WebSocket
open KPX.TheBot.WebSocket.Instance

let accessUrl = "wss://coolqapi.danmaku.org"
let token     = "0194caec-12a2-473d-bc08-962049999446"
let admins =
        [|
            313388419L
            343512452L
        |] |> Set.ofArray

type AuthToken(secret : string) as x =
    let utf8 = Text.Encoding.UTF8
    let key  = utf8.GetBytes(secret)
    let sha  = new System.Security.Cryptography.HMACSHA512(key)
    let mutable token = ""
    let mutable expires = DateTimeOffset.UtcNow

    do
        x.Renew()

    member x.Token = token

    member x.Renew() =
        token <- Guid.NewGuid().ToString()
        expires <- DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5.0)

    member x.Sign =
        sha.ComputeHash(utf8.GetBytes(token))
        |> Convert.ToBase64String

type SudoModule() =
    inherit HandlerModuleBase()
    let mutable allow = false

    override x.MessageHandler _ arg =
        let str = arg.Data.Message.ToString()
        match str.ToLowerInvariant() with
        | s when s.StartsWith("#allowrequest") ->
            if admins.Contains(arg.Data.UserId) then
                x.QuickMessageReply(arg, "已允许加群")
                allow <- true
            else
                x.QuickMessageReply(arg, "朋友你不是狗管理")

        | s when s.StartsWith("#disallowrequest") ->
            if admins.Contains(arg.Data.UserId) then
                x.QuickMessageReply(arg, "已关闭加群")
                allow <- false
            else
                x.QuickMessageReply(arg, "朋友你不是狗管理")

        | _ -> ()

    override x.RequestHandler _ arg =
        match arg.Data with
        | DataType.Event.Request.FriendRequest x ->
            arg.Response <- DataType.Response.FriendAddResponse(allow, "")
        | DataType.Event.Request.GroupRequest x ->
            arg.Response <- DataType.Response.GroupAddResponse(allow, "")

[<EntryPoint>]
let main argv =
    let client = new CqWebSocketClient(new Uri(accessUrl), token)
    client.RegisterModule(new SudoModule())
    client.Connect()
    client.StartListen()

    let req = new Api.GetLoginInfo()
    client.CallApi(req) 

    printfn "当前登用户%i:%s" req.UserId req.Nickname

    Console.ReadLine() |> ignore
    0 // return an integer exit code