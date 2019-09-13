// Learn more about F# at http://fsharp.org
open System
open Mono.Unix
open Mono.Unix.Native
open KPX.FsCqHttp.Instance

let logger = NLog.LogManager.GetCurrentClassLogger()
let accessUrl = "wss://coolqapi.danmaku.org"
let token     = "0194caec-12a2-473d-bc08-962049999446"

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


[<EntryPoint>]
let main argv =
    let client = new CqWebSocketClient(new Uri(accessUrl), token)
    for m in CommandHandlerBase.CommandHandlerBase.AllDefinedModules do 
        logger.Info("正在注册模块{0}", m.GetType().FullName)
        client.RegisterModule(m)
    client.Connect()
    client.StartListen()

    if Type.GetType("Mono.Runtime") <> null then
        UnixSignal.WaitAny(
            [|
                new UnixSignal(Signum.SIGINT)
                new UnixSignal(Signum.SIGTERM)
                new UnixSignal(Signum.SIGQUIT)
                new UnixSignal(Signum.SIGHUP)
            |]) |> ignore
    else
        Console.ReadLine() |> ignore

    client.StopListen()

    Console.WriteLine("Stopping TheBot");
    0 // return an integer exit code